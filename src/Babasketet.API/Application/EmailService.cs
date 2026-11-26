using System.Net.Mail;
using Babasketet.API.Domain;
using Babasketet.API.Infrastructure;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace Babasketet.API.Application;

public sealed record OutboundEmailSettings(
    string Host,
    int Port,
    string? Username,
    string? Password,
    string SecurityMode,
    string FromEmail,
    string FromName,
    bool Enabled);

public interface ISmtpDeliveryClient
{
    Task SendAsync(
        OutboundEmailSettings settings,
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody,
        CancellationToken ct = default);
}

public sealed class MailKitSmtpDeliveryClient : ISmtpDeliveryClient
{
    public async Task SendAsync(
        OutboundEmailSettings settings,
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody,
        CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody ?? string.Empty
        };
        message.Body = builder.ToMessageBody();

        var socketOptions = settings.SecurityMode switch
        {
            EmailSecurityModes.None => SecureSocketOptions.None,
            EmailSecurityModes.StartTls => SecureSocketOptions.StartTls,
            EmailSecurityModes.SslTls => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.Auto
        };

        using var client = new MailKit.Net.Smtp.SmtpClient();
        client.Timeout = 15_000;
        await client.ConnectAsync(settings.Host, settings.Port, socketOptions, ct);
        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, ct);
        }
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}

public sealed class EmailService(
    BabasketetDbContext db,
    UserDirectoryService userDirectory,
    IDataProtectionProvider dataProtection,
    ISmtpDeliveryClient smtpClient,
    TimeProvider timeProvider)
{
    private const string ProtectorPurpose = "Babasketet.SmtpPassword.v1";

    public async Task<EmailConfigDto> GetConfigForAdminAsync(string? rawBearerToken, CancellationToken ct)
    {
        await userDirectory.RequireAdminAsync(rawBearerToken, ct);
        var entity = await db.SystemEmailConfigs.FirstOrDefaultAsync(x => x.Id == SystemEmailConfig.SingletonId, ct);
        return entity is null ? EmptyDto() : ToDto(entity);
    }

    public async Task<EmailConfigDto> SaveConfigForAdminAsync(
        SaveEmailConfigRequest request,
        string? rawBearerToken,
        CancellationToken ct)
    {
        await userDirectory.RequireAdminAsync(rawBearerToken, ct);
        var validated = ValidateAndNormalize(request);

        var entity = await db.SystemEmailConfigs.FirstOrDefaultAsync(x => x.Id == SystemEmailConfig.SingletonId, ct);
        var now = timeProvider.GetUtcNow();

        if (entity is null)
        {
            entity = new SystemEmailConfig
            {
                Id = SystemEmailConfig.SingletonId
            };
            db.SystemEmailConfigs.Add(entity);
        }

        entity.Host = validated.Host;
        entity.Port = validated.Port;
        entity.Username = validated.Username;
        entity.SecurityMode = validated.SecurityMode;
        entity.FromEmail = validated.FromEmail;
        entity.FromName = validated.FromName;
        entity.Enabled = validated.Enabled;
        entity.UpdatedAt = now;

        if (request.ClearPassword)
        {
            entity.EncryptedPassword = null;
        }
        else if (!string.IsNullOrWhiteSpace(request.Password))
        {
            entity.EncryptedPassword = ProtectPassword(request.Password);
        }

        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<TestEmailResultDto> SendTestEmailForAdminAsync(
        TestEmailRequest request,
        string? rawBearerToken,
        CancellationToken ct)
    {
        var admin = await userDirectory.RequireAdminAsync(rawBearerToken, ct);

        var recipient = !string.IsNullOrWhiteSpace(request.ToEmail)
            ? request.ToEmail.Trim()
            : admin.Email;

        if (string.IsNullOrWhiteSpace(recipient) || !IsValidEmail(recipient))
        {
            throw new DomainValidationException(
                "INVALID_TEST_RECIPIENT",
                "Indica una dirección de correo válida para enviar la prueba.");
        }

        OutboundEmailSettings settings;
        if (request.OverrideConfig is not null)
        {
            var existing = await db.SystemEmailConfigs.FirstOrDefaultAsync(x => x.Id == SystemEmailConfig.SingletonId, ct);
            var fallbackPwd = existing?.EncryptedPassword != null
                ? UnprotectPassword(existing.EncryptedPassword)
                : null;
            settings = ValidateAndNormalize(request.OverrideConfig, request.OverrideConfig.ClearPassword ? null : (string.IsNullOrWhiteSpace(request.OverrideConfig.Password) ? fallbackPwd : request.OverrideConfig.Password));
        }
        else
        {
            settings = await LoadActiveSettingsOrThrowAsync(allowDisabled: true, ct);
        }

        var now = timeProvider.GetUtcNow();
        var subject = "Babasketet · Prueba de correo saliente";
        var html = $"""
            <h2>Prueba de configuración SMTP de Babasketet</h2>
            <p>Este mensaje confirma que el servidor <strong>{System.Net.WebUtility.HtmlEncode(settings.Host)}:{settings.Port}</strong> ({System.Net.WebUtility.HtmlEncode(settings.SecurityMode)}) funciona correctamente.</p>
            <p>Remitente: {System.Net.WebUtility.HtmlEncode(settings.FromName)} &lt;{System.Net.WebUtility.HtmlEncode(settings.FromEmail)}&gt;</p>
            <p>Fecha UTC: {now:yyyy-MM-dd HH:mm:ss}Z</p>
            """;
        var text = $"Prueba SMTP Babasketet OK ({settings.Host}:{settings.Port} - {settings.SecurityMode}) @ {now:yyyy-MM-dd HH:mm:ss}Z";

        try
        {
            await smtpClient.SendAsync(settings, recipient, subject, html, text, ct);
        }
        catch (Exception ex) when (ex is not DomainException)
        {
            throw new DomainValidationException(
                "SMTP_TEST_FAILED",
                $"Error al enviar el correo de prueba vía {settings.Host}:{settings.Port}: {ex.Message}");
        }

        return new TestEmailResultDto(
            true,
            recipient,
            $"Correo de prueba enviado correctamente a {recipient}.",
            now);
    }

    public async Task<bool> TrySendPlatformEmailAsync(
        IEnumerable<string> recipients,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken ct = default)
    {
        var list = recipients
            .Where(r => !string.IsNullOrWhiteSpace(r) && IsValidEmail(r.Trim()))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (list.Count == 0) return false;

        var entity = await db.SystemEmailConfigs.FirstOrDefaultAsync(x => x.Id == SystemEmailConfig.SingletonId, ct);
        if (entity is null || !entity.Enabled || string.IsNullOrWhiteSpace(entity.Host) || string.IsNullOrWhiteSpace(entity.FromEmail))
        {
            return false;
        }

        var settings = ToRuntimeSettings(entity);
        foreach (var to in list)
        {
            await smtpClient.SendAsync(settings, to, subject, htmlBody, textBody, ct);
        }
        return true;
    }

    private async Task<OutboundEmailSettings> LoadActiveSettingsOrThrowAsync(bool allowDisabled, CancellationToken ct)
    {
        var entity = await db.SystemEmailConfigs.FirstOrDefaultAsync(x => x.Id == SystemEmailConfig.SingletonId, ct);
        if (entity is null || string.IsNullOrWhiteSpace(entity.Host) || string.IsNullOrWhiteSpace(entity.FromEmail))
        {
            throw new DomainValidationException(
                "EMAIL_NOT_CONFIGURED",
                "El correo saliente aún no está configurado en el panel.");
        }
        if (!allowDisabled && !entity.Enabled)
        {
            throw new DomainValidationException(
                "EMAIL_DISABLED",
                "El envío de correo saliente está deshabilitado.");
        }
        return ToRuntimeSettings(entity);
    }

    private OutboundEmailSettings ToRuntimeSettings(SystemEmailConfig entity) =>
        new(
            entity.Host,
            entity.Port,
            entity.Username,
            entity.EncryptedPassword != null ? UnprotectPassword(entity.EncryptedPassword) : null,
            entity.SecurityMode,
            entity.FromEmail,
            entity.FromName,
            entity.Enabled);

    private OutboundEmailSettings ValidateAndNormalize(SaveEmailConfigRequest request, string? effectivePassword = null)
    {
        var host = request.Host?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(host) || host.Length > 255)
        {
            throw new DomainValidationException("INVALID_SMTP_HOST", "El servidor SMTP es obligatorio (máx. 255 caracteres).");
        }
        if (request.Port is < 1 or > 65535)
        {
            throw new DomainValidationException("INVALID_SMTP_PORT", "El puerto SMTP debe estar entre 1 y 65535.");
        }

        var fromEmail = request.FromEmail?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fromEmail) || !IsValidEmail(fromEmail) || fromEmail.Length > 256)
        {
            throw new DomainValidationException("INVALID_FROM_EMAIL", "La dirección del remitente no es válida.");
        }

        var sec = request.SecurityMode?.Trim() ?? EmailSecurityModes.StartTls;
        var matchedSec = EmailSecurityModes.Allowed.FirstOrDefault(m => string.Equals(m, sec, StringComparison.OrdinalIgnoreCase));
        if (matchedSec is null)
        {
            throw new DomainValidationException("INVALID_SECURITY_MODE", "Modo de seguridad inválido (None, StartTls, SslTls o Auto).");
        }

        var fromName = string.IsNullOrWhiteSpace(request.FromName) ? "Babasketet" : request.FromName.Trim();
        if (fromName.Length > 150)
        {
            throw new DomainValidationException("INVALID_FROM_NAME", "El nombre del remitente no puede superar 150 caracteres.");
        }

        var username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();
        if (username?.Length > 255)
        {
            throw new DomainValidationException("INVALID_SMTP_USERNAME", "El usuario SMTP no puede superar 255 caracteres.");
        }

        return new OutboundEmailSettings(
            host,
            request.Port,
            username,
            effectivePassword ?? (string.IsNullOrWhiteSpace(request.Password) ? null : request.Password),
            matchedSec,
            fromEmail,
            fromName,
            request.Enabled);
    }

    private string ProtectPassword(string raw) =>
        dataProtection.CreateProtector(ProtectorPurpose).Protect(raw);

    private string? UnprotectPassword(string protectedValue)
    {
        try
        {
            return dataProtection.CreateProtector(ProtectorPurpose).Unprotect(protectedValue);
        }
        catch
        {
            return null;
        }
    }

    private static EmailConfigDto EmptyDto() =>
        new(false, string.Empty, 587, null, false, EmailSecurityModes.StartTls, string.Empty, "Babasketet", true, null);

    private static EmailConfigDto ToDto(SystemEmailConfig e) =>
        new(
            !string.IsNullOrWhiteSpace(e.Host) && !string.IsNullOrWhiteSpace(e.FromEmail),
            e.Host,
            e.Port,
            e.Username,
            !string.IsNullOrEmpty(e.EncryptedPassword),
            e.SecurityMode,
            e.FromEmail,
            e.FromName,
            e.Enabled,
            e.UpdatedAt);

    private static bool IsValidEmail(string value)
    {
        try
        {
            var addr = new MailAddress(value);
            return string.Equals(addr.Address, value, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
