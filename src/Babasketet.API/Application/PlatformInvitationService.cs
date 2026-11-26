using System.Security.Cryptography;
using Babasketet.API.Domain;
using Babasketet.API.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Babasketet.API.Application;

public sealed class PlatformInvitationService(
    BabasketetDbContext db,
    UserDirectoryService users,
    EmailService emailService,
    AdminNotificationService notificationService,
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

    public async Task<CheckPlatformInvitationResponse> CheckPublicAsync(
        string code,
        CancellationToken ct = default)
    {
        var normalized = NormalizeCode(code);
        var now = timeProvider.GetUtcNow();
        if (string.IsNullOrEmpty(normalized))
        {
            return new CheckPlatformInvitationResponse(
                normalized,
                "Invalid",
                false,
                false,
                false,
                null,
                "Introduce un código de invitación.");
        }

        var entity = await db.PlatformInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == normalized, ct);
        if (entity is null)
        {
            return new CheckPlatformInvitationResponse(
                normalized,
                "Invalid",
                false,
                false,
                false,
                null,
                "El código de invitación no es válido o no existe.");
        }

        var status = entity.ComputeStatus(now);
        return status switch
        {
            PlatformInvitationStatuses.Active => new CheckPlatformInvitationResponse(
                entity.Code, status, true, true, false, entity.ExpiresAt,
                "Código válido y activo. Regístrate para canjear la invitación."),
            PlatformInvitationStatuses.Expired => new CheckPlatformInvitationResponse(
                entity.Code, status, true, false, true, entity.ExpiresAt,
                "El código de invitación es válido, pero ha expirado."),
            PlatformInvitationStatuses.Cancelled => new CheckPlatformInvitationResponse(
                entity.Code, status, false, false, false, entity.ExpiresAt,
                "Esta invitación ha sido cancelada por un administrador."),
            _ => new CheckPlatformInvitationResponse(
                entity.Code, status, false, false, false, entity.ExpiresAt,
                "Esta invitación ya ha sido canjeada anteriormente.")
        };
    }

    public async Task<RedeemPlatformInvitationResponse> RedeemAsync(
        RedeemPlatformInvitationRequest request,
        string? rawBearerToken,
        CancellationToken ct = default)
    {
        var appUser = await users.SyncAndValidateCallerAsync(rawBearerToken, ct);
        if (appUser is null || !string.Equals(appUser.GlobalRole, AppGlobalRoles.User, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException(
                "Debes registrarte o iniciar sesión como usuario para canjear una invitación.");
        }

        var normalized = NormalizeCode(request.Code);
        var entity = await db.PlatformInvitations
            .FirstOrDefaultAsync(x => x.Code == normalized, ct)
            ?? throw new ResourceNotFoundException("invitación", normalized);

        var now = timeProvider.GetUtcNow();
        if (entity.IsRedeemed)
        {
            if (entity.RedeemedByUserId == appUser.Id)
            {
                return new RedeemPlatformInvitationResponse(
                    true, entity.Code, entity.RedeemedByUserId, entity.RedeemedAt);
            }
            throw new DomainValidationException(
                "INVITATION_ALREADY_REDEEMED",
                "Esta invitación ya fue canjeada por otro usuario.");
        }

        if (entity.IsCancelled)
        {
            throw new DomainValidationException(
                "INVITATION_CANCELLED",
                "Esta invitación fue cancelada.");
        }

        if (entity.IsExpiredAt(now))
        {
            throw new InvitationExpiredException();
        }

        entity.RedeemedAt = now;
        entity.RedeemedByUserId = appUser.Id;
        await db.SaveChangesAsync(ct);

        return new RedeemPlatformInvitationResponse(true, entity.Code, appUser.Id, now);
    }

    public async Task<RequestInvitationRenewalResponse> RequestRenewalAsync(
        RequestInvitationRenewalRequest request,
        CancellationToken ct = default)
    {
        var normalized = NormalizeCode(request.Code);
        var entity = await db.PlatformInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == normalized, ct)
            ?? throw new ResourceNotFoundException("invitación", normalized);

        var now = timeProvider.GetUtcNow();
        if (!entity.IsExpiredAt(now))
        {
            throw new DomainValidationException(
                "INVITATION_RENEWAL_NOT_NEEDED",
                "Solo se puede solicitar renovación para invitaciones válidas que hayan expirado.");
        }

        var requester = string.IsNullOrWhiteSpace(request.RequesterEmail)
            ? "Un usuario invitado"
            : request.RequesterEmail.Trim();
        var noteLine = string.IsNullOrWhiteSpace(request.Note)
            ? string.Empty
            : $"\nNota del solicitante: {request.Note.Trim()}";
        var expiredStr = entity.ExpiresAt.HasValue
            ? entity.ExpiresAt.Value.ToUniversalTime().ToString("dd/MM/yyyy HH:mm 'UTC'")
            : "—";

        var title = $"Solicitud de renovación · {entity.Code}";
        var body = $"{requester} ha solicitado renovar la invitación expirada '{entity.Code}' (expiró el {expiredStr}).{noteLine}\nPuedes reactivarla desde /admin-bo > Invitaciones.";

        await notificationService.CreateAndNotifyAdminsAsync(
            title,
            body,
            "invitation-renewal",
            ct);

        return new RequestInvitationRenewalResponse(
            true,
            $"Se ha enviado la solicitud de renovación para el código {entity.Code} a los administradores.");
    }

    private static string NormalizeCode(string? code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant();

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
