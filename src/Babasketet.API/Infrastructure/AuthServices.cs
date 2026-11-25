using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Babasketet.API.Application;
using Babasketet.API.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Babasketet.API.Infrastructure;

public sealed class DeviceTokenOptions
{
    public const string SectionName = "Auth";
    public string DeviceTokenSigningKey { get; set; } =
        "Babasketet_Dev_Signing_Key_Change_In_Prod_Min_32_Bytes!";
    public string OidcIssuer { get; set; } = "http://localhost:8085";
    public string OidcAuthority { get; set; } = "http://zitadel:8080";
    public string OidcAudience { get; set; } = "babasketet";
}

public sealed class DeviceTokenService(DeviceTokenOptions options) : IDeviceTokenService
{
    public const string Issuer = "babasketet-device";
    public const string Audience = "babasketet-api";
    public const string DeviceIdClaim = "device_id";

    private readonly SymmetricSecurityKey _signingKey =
        new(Encoding.UTF8.GetBytes(options.DeviceTokenSigningKey.PadRight(32, '_')));

    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = _signingKey,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2)
    };

    public string IssueToken(Guid deviceId)
    {
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(DeviceIdClaim, deviceId.ToString("D")),
            new Claim(JwtRegisteredClaimNames.Sub, $"device:{deviceId:D}")
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddYears(2),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Guid? TryValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, CreateValidationParameters(), out _);
            var raw = principal.FindFirstValue(DeviceIdClaim);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class HttpCallerContextAccessor(IHttpContextAccessor httpContextAccessor) : ICallerContextAccessor
{
    public CallerIdentity Current
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return new CallerIdentity(null, null, false);
            }

            var deviceRaw = user.FindFirstValue(DeviceTokenService.DeviceIdClaim);
            if (Guid.TryParse(deviceRaw, out var deviceId))
            {
                return new CallerIdentity(null, deviceId, false);
            }

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? user.FindFirstValue("sub");

            var isPremium = string.Equals(user.FindFirstValue("plan"), "premium", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(user.FindFirstValue("premium"), "true", StringComparison.OrdinalIgnoreCase);

            return new CallerIdentity(userId, null, isPremium);
        }
    }
}
