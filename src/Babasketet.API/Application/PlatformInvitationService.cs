using System.Security.Cryptography;
using Babasketet.API.Domain;
using Babasketet.API.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Babasketet.API.Application;

public sealed class PlatformInvitationService(
    BabasketetDbContext db,
    UserDirectoryService users,
    EmailService emailService,
    TimeProvider timeProvider)
{
    private static readonly char[] CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();
    private static readonly HashSet<int> AllowedDaySteps = [1, 7, 15, 30];

    public async Task<IReadOnlyList<PlatformInvitationDto>> GetListForAdminAsync(
        string? dateRange,
        string? statuses,
        string? bearerToken,
        CancellationToken ct = default)
    {
        await users.RequireAdminAsync(bearerToken, ct);
        var now = timeProvider.GetUtcNow();
        var cutoff = ResolveDateCutoff(dateRange, now);

        var query = db.PlatformInvitations
            .AsNoTracking()
            .Include(x => x.RedeemedByUser)
            .AsQueryable();

        if (cutoff.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= cutoff.Value);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var allowedStatuses = ParseStatusFilter(statuses);

        return items
            .Where(x => allowedStatuses is null || allowedStatuses.Contains(x.ComputeStatus(now)))
            .Select(x => ToDto(x, now))
            .ToList();
    }

    public async Task<PlatformInvitationDto> CreateForAdminAsync(
        CreatePlatformInvitationRequest request,
        string? bearerToken,
        CancellationToken ct = default)
    {
        await users.RequireAdminAsync(bearerToken, ct);
        var now = timeProvider.GetUtcNow();

        DateTimeOffset? expiresAt;
        if (request.IsIndefinite)
        {
            expiresAt = null;
        }
        else if (request.ExpiresAt.HasValue)
        {
            if (request.ExpiresAt.Value <= now)
            {
                throw new DomainValidationException(
                    "INVALID_EXPIRATION",
                    "La fecha de expiración debe ser posterior al momento actual.");
            }
            expiresAt = request.ExpiresAt.Value.ToUniversalTime();
        }
        else
        {
            expiresAt = now.AddDays(7);
        }

        var code = await GenerateUniqueCodeAsync(ct);
        var entity = new PlatformInvitation
        {
            Id = Guid.NewGuid(),
            Code = code,
            CreatedAt = now,
            ExpiresAt = expiresAt
        };

        db.PlatformInvitations.Add(entity);
        await db.SaveChangesAsync(ct);

        return ToDto(entity, now);
    }

    public async Task<PlatformInvitationDto> CancelAsync(
        Guid id,
        string? bearerToken,
        CancellationToken ct = default)
    {
        await users.RequireAdminAsync(bearerToken, ct);
        var now = timeProvider.GetUtcNow();
        var entity = await FindOrThrowAsync(id, ct);

        if (entity.IsRedeemed)
        {
            throw new DomainValidationException(
                "INVITATION_ALREADY_REDEEMED",
                "No se puede cancelar una invitación que ya ha sido canjeada.");
        }

        entity.CancelledAt ??= now;
        await db.SaveChangesAsync(ct);
        return ToDto(entity, now);
    }

    public async Task<PlatformInvitationDto> RenewAsync(
        Guid id,
        RenewPlatformInvitationRequest request,
        string? bearerToken,
        CancellationToken ct = default)
    {
        await users.RequireAdminAsync(bearerToken, ct);
        var now = timeProvider.GetUtcNow();
        var entity = await FindOrThrowAsync(id, ct);

        // Solo se puede renovar si NO ha caducado ni sido cancelada/canjeada
        if (!entity.IsActiveAt(now))
        {
            throw new DomainValidationException(
                "INVITATION_CANNOT_RENEW",
                "Solo se pueden renovar invitaciones que sigan activas y no hayan caducado ni sido canceladas o canjeadas.");
        }

        if (request.IsIndefinite)
        {
            entity.ExpiresAt = null;
        }
        else
        {
            if (!AllowedDaySteps.Contains(request.Days))
            {
                throw new DomainValidationException(
                    "INVALID_RENEW_DAYS",
                    "Los plazos válidos para renovar son 1, 7, 15 o 30 días (o indefinida).");
            }
            entity.ExpiresAt = (entity.ExpiresAt ?? now).AddDays(request.Days);
        }

        await db.SaveChangesAsync(ct);
        return ToDto(entity, now);
    }

    public async Task<PlatformInvitationDto> ReactivateAsync(
        Guid id,
        ReactivatePlatformInvitationRequest request,
        string? bearerToken,
        CancellationToken ct = default)
    {
        await users.RequireAdminAsync(bearerToken, ct);
        var now = timeProvider.GetUtcNow();
        var entity = await FindOrThrowAsync(id, ct);

        if (entity.IsRedeemed)
        {
            throw new DomainValidationException(
                "INVITATION_ALREADY_REDEEMED",
                "No se puede reactivar una invitación que ya fue canjeada.");
        }

        if (!AllowedDaySteps.Contains(request.Days))
        {
            throw new DomainValidationException(
                "INVALID_REACTIVATE_DAYS",
                "Los plazos válidos de reactivación son 1, 7, 15 o 30 días.");
        }

        entity.CancelledAt = null;
        entity.ExpiresAt = now.AddDays(request.Days);

        await db.SaveChangesAsync(ct);
        return ToDto(entity, now);
    }

    public async Task<SendPlatformInvitationEmailsResultDto> SendByEmailAsync(
        Guid id,
        SendPlatformInvitationEmailsRequest request,
        string? bearerToken,
        CancellationToken ct = default)
    {
        await users.RequireAdminAsync(bearerToken, ct);
        var now = timeProvider.GetUtcNow();
        var entity = await FindOrThrowAsync(id, ct);

        if (!entity.IsActiveAt(now))
        {
            throw new DomainValidationException(
                "INVITATION_NOT_ACTIVE",
                "Solo se pueden enviar por email invitaciones activas y no caducadas.");
        }

        var validRecipients = new List<string>();
        foreach (var raw in request.Emails ?? [])
        {
            var email = raw.Trim();
            if (string.IsNullOrEmpty(email)) continue;
            if (!IsValidEmail(email))
            {
                throw new DomainValidationException(
                    "INVALID_EMAIL_ADDRESS",
                    $"Dirección de correo no válida: {email}");
            }
            if (!validRecipients.Contains(email, StringComparer.OrdinalIgnoreCase))
            {
                validRecipients.Add(email);
            }
        }

        if (validRecipients.Count == 0)
        {
            throw new DomainValidationException(
                "NO_RECIPIENTS",
                "Introduce al menos una dirección de correo válida.");
        }

        var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl)
            ? "https://babasketet.local"
            : request.BaseUrl.Trim().TrimEnd('/');
        var redeemUrl = $"{baseUrl}/invitation-redeem/{entity.Code}";
        var expiryText = entity.ExpiresAt.HasValue
            ? $"{entity.ExpiresAt.Value.ToUniversalTime():dd/MM/yyyy HH:mm} UTC"
            : "Sin caducidad (indefinida)";

        var subject = $"Invitación a Babasketet · Código {entity.Code}";
        var html = $"""
            <h2>Has recibido una invitación a Babasketet</h2>
            <p>Usa el siguiente código de invitación para crear tu cuenta y empezar a gestionar tu casa:</p>
            <p><strong>Código:</strong> <code>{System.Net.WebUtility.HtmlEncode(entity.Code)}</code></p>
            <p><strong>Enlace directo:</strong> <a href="{System.Net.WebUtility.HtmlEncode(redeemUrl)}">{System.Net.WebUtility.HtmlEncode(redeemUrl)}</a></p>
            <p><small>Validez: {System.Net.WebUtility.HtmlEncode(expiryText)}</small></p>
            """;
        var text = $"Has recibido una invitación a Babasketet.\nCódigo: {entity.Code}\nEnlace: {redeemUrl}\nValidez: {expiryText}";

        var sent = await emailService.TrySendPlatformEmailAsync(validRecipients, subject, html, text, ct);
        if (!sent)
        {
            throw new DomainValidationException(
                "EMAIL_NOT_CONFIGURED",
                "El envío por correo falló o el SMTP saliente no está configurado/activado.");
        }

        return new SendPlatformInvitationEmailsResultDto(
            validRecipients.Count,
            validRecipients,
            $"Invitación enviada a {validRecipients.Count} destinatario(s).");
    }

    private async Task<PlatformInvitation> FindOrThrowAsync(Guid id, CancellationToken ct) =>
        await db.PlatformInvitations
            .Include(x => x.RedeemedByUser)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ResourceNotFoundException("invitación", id);

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var chars = new char[8];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
            }
            var candidate = $"BBK-{new string(chars)}";
            var exists = await db.PlatformInvitations.AnyAsync(x => x.Code == candidate, ct);
            if (!exists) return candidate;
        }
        return $"BBK-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
    }

    private static DateTimeOffset? ResolveDateCutoff(string? dateRange, DateTimeOffset now) =>
        dateRange?.Trim().ToLowerInvariant() switch
        {
            "year" => now.AddYears(-1),
            "6months" => now.AddMonths(-6),
            "3months" => now.AddMonths(-3),
            "month" => now.AddMonths(-1),
            "week" => now.AddDays(-7),
            "24h" => now.AddHours(-24),
            _ => null
        };

    private static HashSet<string>? ParseStatusFilter(string? statuses)
    {
        if (string.IsNullOrWhiteSpace(statuses))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { PlatformInvitationStatuses.Active };
        }

        var parts = statuses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Any(p => p.Equals("All", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parts)
        {
            if (PlatformInvitationStatuses.Allowed.TryGetValue(p, out var canonical))
            {
                set.Add(canonical);
            }
        }
        return set.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { PlatformInvitationStatuses.Active }
            : set;
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(value);
            return string.Equals(addr.Address, value, StringComparison.OrdinalIgnoreCase) && value.Contains('.');
        }
        catch
        {
            return false;
        }
    }

    internal static PlatformInvitationDto ToDto(PlatformInvitation x, DateTimeOffset now) =>
        new(
            x.Id,
            x.Code,
            x.ComputeStatus(now),
            x.CreatedAt,
            x.ExpiresAt,
            x.ExpiresAt is null,
            x.CancelledAt,
            x.RedeemedAt,
            x.RedeemedByUser is not null
                ? new PlatformInvitationRedeemerDto(
                    x.RedeemedByUser.Id,
                    x.RedeemedByUser.DisplayName ?? x.RedeemedByUser.Email ?? x.RedeemedByUser.Id.ToString(),
                    x.RedeemedByUser.Email)
                : null);
}
