using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
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
    public string? OidcClientIdFile { get; set; }
    public string? OidcProjectIdFile { get; set; }
    public string AdminSeedEmail { get; set; } = "jonathanvivero@gmail.com";

    public HashSet<string> GetValidAudiences()
    {
        var list = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(OidcAudience))
        {
            list.Add(OidcAudience.Trim());
        }
        foreach (var path in new[] { OidcClientIdFile, OidcProjectIdFile })
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                var content = File.ReadAllText(path).Trim();
                if (!string.IsNullOrEmpty(content))
                {
                    list.Add(content);
                }
            }
        }
        return list;
    }
}

public sealed class ZitadelBackchannelHandler(Uri publicIssuerUri, Uri internalAuthorityUri) : HttpClientHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri != null &&
            string.Equals(request.RequestUri.Host, publicIssuerUri.Host, StringComparison.OrdinalIgnoreCase) &&
            request.RequestUri.Port == publicIssuerUri.Port)
        {
            var rewritten = new UriBuilder(request.RequestUri)
            {
                Scheme = internalAuthorityUri.Scheme,
                Host = internalAuthorityUri.Host,
                Port = internalAuthorityUri.Port
            };
            request.RequestUri = rewritten.Uri;
        }

        request.Headers.Host = publicIssuerUri.Authority;
        return base.SendAsync(request, cancellationToken);
    }
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

            var email = user.FindFirstValue(ClaimTypes.Email)
                        ?? user.FindFirstValue("email")
                        ?? user.FindFirstValue("preferred_username");

            var displayName = user.FindFirstValue("name")
                              ?? user.FindFirstValue(ClaimTypes.Name);

            var roles = ExtractRoles(user);

            var isPremium = string.Equals(user.FindFirstValue("plan"), "premium", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(user.FindFirstValue("premium"), "true", StringComparison.OrdinalIgnoreCase);

            return new CallerIdentity(userId, null, isPremium, email, displayName, roles);
        }
    }

    private static IReadOnlyCollection<string> ExtractRoles(ClaimsPrincipal principal)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claim in principal.Claims)
        {
            if (claim.Type == ClaimTypes.Role ||
                string.Equals(claim.Type, "role", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, "roles", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(claim.Value))
                {
                    roles.Add(claim.Value.Trim());
                }
            }
            else if (claim.Type.StartsWith("urn:zitadel:iam:org:project:", StringComparison.OrdinalIgnoreCase) &&
                     claim.Type.EndsWith(":roles", StringComparison.OrdinalIgnoreCase))
            {
                TryAddZitadelRoleKeys(claim.Value, roles);
            }
        }

        return roles.ToArray();
    }

    private static void TryAddZitadelRoleKeys(string rawJson, HashSet<string> target)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return;
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (!string.IsNullOrWhiteSpace(prop.Name))
                    {
                        target.Add(prop.Name.Trim());
                    }
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var val = item.GetString();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        target.Add(val.Trim());
                    }
                }
            }
        }
        catch
        {
            // Ignore malformed role claims
        }
    }
}
