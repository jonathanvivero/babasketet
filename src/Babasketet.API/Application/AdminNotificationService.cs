using System.Net;
using Babasketet.API.Domain;
using Babasketet.API.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Babasketet.API.Application;

public sealed class AdminNotificationService(
    BabasketetDbContext db,
    UserDirectoryService userDirectory,
    EmailService emailService,
    DeviceTokenOptions authOptions,
    TimeProvider timeProvider,
    ILogger<AdminNotificationService> logger)
{
    public async Task<AdminNotificationsPageDto> GetPagedForAdminAsync(
        string? statusFilter,
        int page,
        int pageSize,
        string? rawBearerToken,
        CancellationToken ct)
    {
        await userDirectory.RequireAdminAsync(rawBearerToken, ct);

        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is < 1 or > 50 ? 10 : pageSize;

        var query = db.AdminNotifications.AsQueryable();

        var filterRaw = string.IsNullOrWhiteSpace(statusFilter) ? NotificationStatuses.New : statusFilter.Trim();
        if (!string.Equals(filterRaw, "All", StringComparison.OrdinalIgnoreCase))
        {
            var requestedStatuses = filterRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => NotificationStatuses.Allowed.FirstOrDefault(a => string.Equals(a, s, StringComparison.OrdinalIgnoreCase)))
                .Where(s => s is not null)
                .Select(s => s!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (requestedStatuses.Count == 0)
            {
                requestedStatuses.Add(NotificationStatuses.New);
            }

            query = query.Where(n => requestedStatuses.Contains(n.Status));
        }

        var totalCount = await query.CountAsync(ct);
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)safePageSize);
        if (safePage > totalPages) safePage = totalPages;

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenBy(n => n.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(n => ToDto(n))
            .ToListAsync(ct);

        var newCount = await db.AdminNotifications.CountAsync(n => n.Status == NotificationStatuses.New, ct);

        return new AdminNotificationsPageDto(items, safePage, safePageSize, totalCount, totalPages, newCount);
    }

    public async Task<AdminNotificationDto> CreateFromAdminAsync(
        CreateAdminNotificationRequest request,
        string? rawBearerToken,
        CancellationToken ct)
    {
        await userDirectory.RequireAdminAsync(rawBearerToken, ct);
        return await CreateAndNotifyAdminsAsync(request.Title, request.Body, request.Category, ct);
    }

    public async Task<AdminNotificationDto> CreateAndNotifyAdminsAsync(
        string title,
        string body,
        string? category,
        CancellationToken ct = default)
    {
        var cleanTitle = title?.Trim() ?? string.Empty;
        var cleanBody = body?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cleanTitle) || cleanTitle.Length > 200)
        {
            throw new DomainValidationException("INVALID_NOTIFICATION_TITLE", "El título de la notificación es obligatorio (máx. 200 caracteres).");
        }
        if (string.IsNullOrWhiteSpace(cleanBody))
        {
            throw new DomainValidationException("INVALID_NOTIFICATION_BODY", "El cuerpo de la notificación es obligatorio.");
        }

        var cleanCategory = string.IsNullOrWhiteSpace(category) ? "system" : category.Trim();
        if (cleanCategory.Length > 64) cleanCategory = cleanCategory[..64];

        var entity = new AdminNotification
        {
            Id = Guid.NewGuid(),
            Title = cleanTitle,
            Body = cleanBody,
            Category = cleanCategory,
            Status = NotificationStatuses.New,
            CreatedAt = timeProvider.GetUtcNow(),
            ReadAt = null,
            ArchivedAt = null
        };

        db.AdminNotifications.Add(entity);
        await db.SaveChangesAsync(ct);

        await NotifyAllAdminEmailsAsync(entity, ct);
        return ToDto(entity);
    }

    public async Task<AdminNotificationDto> UpdateStatusForAdminAsync(
        Guid notificationId,
        UpdateNotificationStatusRequest request,
        string? rawBearerToken,
        CancellationToken ct)
    {
        await userDirectory.RequireAdminAsync(rawBearerToken, ct);

        var target = NotificationStatuses.Allowed
            .FirstOrDefault(s => string.Equals(s, request.Status?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainValidationException("INVALID_NOTIFICATION_STATUS", "Estado inválido (New, Read o Archived).");

        var entity = await db.AdminNotifications.FirstOrDefaultAsync(n => n.Id == notificationId, ct)
                     ?? throw new ResourceNotFoundException("notificación", notificationId);

        var now = timeProvider.GetUtcNow();
        entity.Status = target;
        if (target == NotificationStatuses.New)
        {
            entity.ReadAt = null;
            entity.ArchivedAt = null;
        }
        else if (target == NotificationStatuses.Read)
        {
            entity.ReadAt ??= now;
            entity.ArchivedAt = null;
        }
        else if (target == NotificationStatuses.Archived)
        {
            entity.ReadAt ??= now;
            entity.ArchivedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    private async Task NotifyAllAdminEmailsAsync(AdminNotification entity, CancellationToken ct)
    {
        try
        {
            var adminEmails = await db.AppUsers
                .Where(u => u.IsAdmin && u.Status == AppUserStatuses.Active && !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => u.Email!)
                .ToListAsync(ct);

            if (!string.IsNullOrWhiteSpace(authOptions.AdminSeedEmail))
            {
                var seedBlocked = await db.AppUsers.AnyAsync(
                    u => u.Email != null &&
                         u.Email.ToLower() == authOptions.AdminSeedEmail.ToLower() &&
                         u.Status == AppUserStatuses.Blocked,
                    ct);
                if (!seedBlocked)
                {
                    adminEmails.Add(authOptions.AdminSeedEmail.Trim());
                }
            }

            var html = $"""
                <h2>Nueva notificación en Babasketet</h2>
                <p><strong>{WebUtility.HtmlEncode(entity.Title)}</strong></p>
                <p>{WebUtility.HtmlEncode(entity.Body)}</p>
                <p><small>Categoría: {WebUtility.HtmlEncode(entity.Category)} · Fecha UTC: {entity.CreatedAt:yyyy-MM-dd HH:mm:ss}Z</small></p>
                """;
            var text = $"[Babasketet] {entity.Title}\n\n{entity.Body}\n\nCategoría: {entity.Category} ({entity.CreatedAt:yyyy-MM-dd HH:mm:ss}Z)";

            await emailService.TrySendPlatformEmailAsync(
                adminEmails,
                $"[Babasketet] {entity.Title}",
                html,
                text,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo enviar el correo de la notificación {NotificationId}.", entity.Id);
        }
    }

    private static AdminNotificationDto ToDto(AdminNotification n) =>
        new(n.Id, n.Title, n.Body, n.Category, n.Status, n.CreatedAt, n.ReadAt, n.ArchivedAt);
}
