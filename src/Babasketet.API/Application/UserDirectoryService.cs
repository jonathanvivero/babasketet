using System.Net.Http.Headers;
using System.Text.Json;
using Babasketet.API.Domain;
using Babasketet.API.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Babasketet.API.Application;

public sealed class UserDirectoryService(
    BabasketetDbContext db,
    ICallerContextAccessor callerAccessor,
    DeviceTokenOptions authOptions,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider)
{
    public async Task<AppUser?> SyncAndValidateCallerAsync(string? rawBearerToken, CancellationToken ct)
    {
        var caller = callerAccessor.Current;
        if (!caller.IsRegistered && !caller.IsAnonymous)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        AppUser? user;

        if (caller.IsRegistered && !string.IsNullOrWhiteSpace(caller.UserId))
        {
            user = await db.AppUsers.FirstOrDefaultAsync(u => u.ZitadelUserId == caller.UserId, ct);
            var email = caller.Email?.Trim();
            var displayName = caller.DisplayName?.Trim();

            if ((string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(displayName)) &&
                !string.IsNullOrWhiteSpace(rawBearerToken) &&
                !string.IsNullOrWhiteSpace(authOptions.OidcAuthority) &&
                authOptions.OidcAuthority.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                !authOptions.OidcAuthority.Contains("test-zitadel.local", StringComparison.OrdinalIgnoreCase))
            {
                var info = await TryFetchOidcUserInfoAsync(rawBearerToken, ct);
                email ??= info.Email;
                displayName ??= info.DisplayName;
            }

            var isAdminFromClaimOrSeed = caller.HasRole("Admin") ||
                                         (!string.IsNullOrWhiteSpace(email) &&
                                          string.Equals(email, authOptions.AdminSeedEmail, StringComparison.OrdinalIgnoreCase));

            if (user is null)
            {
                user = new AppUser
                {
                    Id = Guid.NewGuid(),
                    ZitadelUserId = caller.UserId,
                    DeviceId = null,
                    GlobalRole = AppGlobalRoles.User,
                    IsAdmin = isAdminFromClaimOrSeed,
                    Status = AppUserStatuses.Active,
                    Email = email,
                    DisplayName = displayName ?? email ?? caller.UserId,
                    AuthMethod = "zitadel",
                    CustomPropertiesJson = "{}",
                    CreatedAt = now,
                    LastSeenAt = now
                };
                db.AppUsers.Add(user);
            }
            else
            {
                user.GlobalRole = AppGlobalRoles.User;
                user.DeviceId = null;
                user.AuthMethod = "zitadel";
                if (!string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(user.Email))
                {
                    user.Email = email;
                }
                if (!string.IsNullOrWhiteSpace(displayName) &&
                    (string.IsNullOrWhiteSpace(user.DisplayName) || user.DisplayName == caller.UserId))
                {
                    user.DisplayName = displayName;
                }
                if (isAdminFromClaimOrSeed && !user.IsAdmin)
                {
                    user.IsAdmin = true;
                }
                user.LastSeenAt = now;
            }

            await db.SaveChangesAsync(ct);
        }
        else if (caller.DeviceId.HasValue)
        {
            user = await db.AppUsers.FirstOrDefaultAsync(u => u.DeviceId == caller.DeviceId.Value, ct);
            if (user is null)
            {
                user = new AppUser
                {
                    Id = Guid.NewGuid(),
                    ZitadelUserId = null,
                    DeviceId = caller.DeviceId.Value,
                    GlobalRole = AppGlobalRoles.Guest,
                    IsAdmin = false,
                    Status = AppUserStatuses.Active,
                    Email = null,
                    DisplayName = $"Invitado {caller.DeviceId.Value.ToString("D")[..8]}",
                    AuthMethod = "anonymous",
                    CustomPropertiesJson = "{}",
                    CreatedAt = now,
                    LastSeenAt = now
                };
                db.AppUsers.Add(user);
            }
            else
            {
                user.LastSeenAt = now;
            }

            await db.SaveChangesAsync(ct);
        }
        else
        {
            return null;
        }

        if (string.Equals(user.Status, AppUserStatuses.Blocked, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserBlockedException();
        }

        return user;
    }

    public async Task<AppUser> EnsureAnonymousGuestRecordedAsync(Guid deviceId, CancellationToken ct)
    {
        var existing = await db.AppUsers.FirstOrDefaultAsync(u => u.DeviceId == deviceId, ct);
        if (existing is not null)
        {
            if (string.Equals(existing.Status, AppUserStatuses.Blocked, StringComparison.OrdinalIgnoreCase))
            {
                throw new UserBlockedException();
            }
            existing.LastSeenAt = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return existing;
        }

        var now = timeProvider.GetUtcNow();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            ZitadelUserId = null,
            DeviceId = deviceId,
            GlobalRole = AppGlobalRoles.Guest,
            IsAdmin = false,
            Status = AppUserStatuses.Active,
            Email = null,
            DisplayName = $"Invitado {deviceId.ToString("D")[..8]}",
            AuthMethod = "anonymous",
            CustomPropertiesJson = "{}",
            CreatedAt = now,
            LastSeenAt = now
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task UpgradeGuestToUserAsync(Guid deviceId, CallerIdentity caller, CancellationToken ct)
    {
        if (!caller.IsRegistered || string.IsNullOrWhiteSpace(caller.UserId))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var guestUser = await db.AppUsers.FirstOrDefaultAsync(u => u.DeviceId == deviceId, ct);
        var registeredUser = await db.AppUsers.FirstOrDefaultAsync(u => u.ZitadelUserId == caller.UserId, ct);

        if (guestUser is not null && registeredUser is null)
        {
            guestUser.ZitadelUserId = caller.UserId;
            guestUser.DeviceId = null;
            guestUser.GlobalRole = AppGlobalRoles.User;
            guestUser.AuthMethod = "zitadel";
            if (!string.IsNullOrWhiteSpace(caller.Email))
            {
                guestUser.Email = caller.Email;
            }
            if (!string.IsNullOrWhiteSpace(caller.DisplayName))
            {
                guestUser.DisplayName = caller.DisplayName;
            }
            else if (guestUser.DisplayName?.StartsWith("Invitado ", StringComparison.Ordinal) == true)
            {
                guestUser.DisplayName = caller.Email ?? caller.UserId;
            }
            if (caller.HasRole("Admin") ||
                string.Equals(guestUser.Email, authOptions.AdminSeedEmail, StringComparison.OrdinalIgnoreCase))
            {
                guestUser.IsAdmin = true;
            }
            guestUser.LastSeenAt = now;
        }
        else if (guestUser is not null && registeredUser is not null && guestUser.Id != registeredUser.Id)
        {
            if (string.Equals(guestUser.Status, AppUserStatuses.Blocked, StringComparison.OrdinalIgnoreCase))
            {
                registeredUser.Status = AppUserStatuses.Blocked;
            }
            db.AppUsers.Remove(guestUser);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<AppUser> RequireAdminAsync(string? rawBearerToken, CancellationToken ct)
    {
        var caller = callerAccessor.Current;
        if (!caller.IsRegistered || string.IsNullOrWhiteSpace(caller.UserId))
        {
            throw new ForbiddenAccessException("Solo el rol Admin puede acceder al panel de administración.");
        }

        var user = await SyncAndValidateCallerAsync(rawBearerToken, ct)
                   ?? throw new ForbiddenAccessException("Solo el rol Admin puede acceder al panel de administración.");

        var isAdmin = user.IsAdmin ||
                      caller.HasRole("Admin") ||
                      (!string.IsNullOrWhiteSpace(user.Email) &&
                       string.Equals(user.Email, authOptions.AdminSeedEmail, StringComparison.OrdinalIgnoreCase));

        if (!isAdmin)
        {
            throw new ForbiddenAccessException("Solo el rol Admin puede acceder al panel de administración.");
        }

        return user;
    }

    public async Task<MeResponse> GetMeAsync(string? rawBearerToken, CancellationToken ct)
    {
        var caller = callerAccessor.Current;
        if (!caller.IsRegistered && !caller.IsAnonymous)
        {
            return new MeResponse(
                null,
                null,
                null,
                AppGlobalRoles.Guest,
                false,
                false,
                AppUserStatuses.Active,
                null,
                "Invitado",
                [AppGlobalRoles.Guest],
                []);
        }

        var user = await SyncAndValidateCallerAsync(rawBearerToken, ct);
        if (user is null)
        {
            return new MeResponse(
                null,
                null,
                null,
                AppGlobalRoles.Guest,
                false,
                false,
                AppUserStatuses.Active,
                null,
                "Invitado",
                [AppGlobalRoles.Guest],
                []);
        }

        var memberships = await db.HouseMembers
            .Include(m => m.House)
            .Where(m =>
                (user.ZitadelUserId != null && m.UserId == user.ZitadelUserId) ||
                (user.DeviceId != null && m.DeviceId == user.DeviceId))
            .OrderBy(m => m.JoinedAt)
            .Select(m => new MeHouseRoleDto(m.HouseId, m.House.Name, m.Role))
            .ToListAsync(ct);

        var isAuthenticated = user.GlobalRole == AppGlobalRoles.User && !string.IsNullOrWhiteSpace(user.ZitadelUserId);
        var effectiveRoles = ComputeEffectiveRoles(user, memberships.Any(m => m.HouseRole == HouseRoles.Owner));

        return new MeResponse(
            user.Id,
            user.ZitadelUserId,
            user.DeviceId,
            user.GlobalRole,
            isAuthenticated,
            user.IsAdmin,
            user.Status,
            user.Email,
            user.DisplayName,
            effectiveRoles,
            memberships);
    }

    public static IReadOnlyList<string> ComputeEffectiveRoles(AppUser user, bool isAnyHouseOwner)
    {
        if (user.GlobalRole == AppGlobalRoles.Guest || string.IsNullOrWhiteSpace(user.ZitadelUserId))
        {
            return [AppGlobalRoles.Guest];
        }

        var roles = new List<string> { AppGlobalRoles.User };
        if (isAnyHouseOwner)
        {
            roles.Add("Owner");
        }
        if (user.IsAdmin)
        {
            roles.Add("Admin");
        }
        return roles;
    }

    private async Task<(string? Email, string? DisplayName)> TryFetchOidcUserInfoAsync(string bearerToken, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("OidcUserInfo");
            var endpoint = $"{authOptions.OidcAuthority.TrimEnd('/')}/oidc/v1/userinfo";
            using var req = new HttpRequestMessage(HttpMethod.Get, endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            if (!string.IsNullOrWhiteSpace(authOptions.OidcIssuer) &&
                Uri.TryCreate(authOptions.OidcIssuer, UriKind.Absolute, out var issuerUri))
            {
                req.Headers.Host = issuerUri.Authority;
            }

            using var res = await client.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                return (null, null);
            }

            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var email = root.TryGetProperty("email", out var em) ? em.GetString() : null;
            var name = root.TryGetProperty("name", out var nm) ? nm.GetString() : null;
            return (email, name);
        }
        catch
        {
            return (null, null);
        }
    }
}
