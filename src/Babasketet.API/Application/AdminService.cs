using System.Globalization;
using System.Text;
using System.Text.Json;
using Babasketet.API.Domain;
using Babasketet.API.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Babasketet.API.Application;

public sealed class AdminService(
    BabasketetDbContext db,
    UserDirectoryService userDirectory,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<AdminHouseSummaryDto>> GetHousesAsync(string? rawBearerToken, CancellationToken ct)
    {
        await userDirectory.RequireAdminAsync(rawBearerToken, ct);

        var houses = await db.Houses
            .Include(h => h.ActiveStore)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(ct);

        var memberCounts = await db.HouseMembers
            .GroupBy(m => m.HouseId)
            .Select(g => new { HouseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.HouseId, x => x.Count, ct);

        var productCounts = await db.Products
            .GroupBy(p => p.HouseId)
            .Select(g => new { HouseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.HouseId, x => x.Count, ct);

        var listCounts = await db.ShoppingListItems
            .GroupBy(i => i.HouseId)
            .Select(g => new { HouseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.HouseId, x => x.Count, ct);

        var basketCounts = await db.BasketItems
            .GroupBy(i => i.HouseId)
            .Select(g => new { HouseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.HouseId, x => x.Count, ct);

        var purchaseCounts = await db.Purchases
            .GroupBy(p => p.HouseId)
            .Select(g => new { HouseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.HouseId, x => x.Count, ct);

        return houses.Select(h => new AdminHouseSummaryDto(
            h.Id,
            h.Name,
            h.ActiveStore?.Name ?? "Default",
            memberCounts.GetValueOrDefault(h.Id),
            productCounts.GetValueOrDefault(h.Id),
            listCounts.GetValueOrDefault(h.Id),
            basketCounts.GetValueOrDefault(h.Id),
            purchaseCounts.GetValueOrDefault(h.Id),
            h.CreatedAt)).ToList();
    }

    public async Task<AdminHouseDetailDto> GetHouseDetailAsync(Guid houseId, string? rawBearerToken, CancellationToken ct)
    {
        await userDirectory.RequireAdminAsync(rawBearerToken, ct);
        await BackfillOrphanAppUsersAsync(ct);

        var house = await db.Houses
            .Include(h => h.ActiveStore)
            .FirstOrDefaultAsync(h => h.Id == houseId, ct)
            ?? throw new ResourceNotFoundException("casa", houseId);

        var memberships = await db.HouseMembers
            .Where(m => m.HouseId == houseId)
            .OrderByDescending(m => m.Role == HouseRoles.Owner)
            .ThenBy(m => m.JoinedAt)
            .ToListAsync(ct);

        var userIds = memberships.Where(m => m.UserId != null).Select(m => m.UserId!).Distinct().ToList();
        var deviceIds = memberships.Where(m => m.DeviceId.HasValue).Select(m => m.DeviceId!.Value).Distinct().ToList();

        var appUsersByZitadel = await db.AppUsers
            .Where(u => u.ZitadelUserId != null && userIds.Contains(u.ZitadelUserId))
            .ToDictionaryAsync(u => u.ZitadelUserId!, ct);

        var appUsersByDevice = await db.AppUsers
            .Where(u => u.DeviceId.HasValue && deviceIds.Contains(u.DeviceId.Value))
            .ToDictionaryAsync(u => u.DeviceId!.Value, ct);

        var membersDto = memberships.Select(m =>
        {
            AppUser? appUser = null;
            if (m.UserId != null) appUsersByZitadel.TryGetValue(m.UserId, out appUser);
            else if (m.DeviceId.HasValue) appUsersByDevice.TryGetValue(m.DeviceId.Value, out appUser);

            var displayName = appUser?.DisplayName
                              ?? appUser?.Email
                              ?? m.UserId
                              ?? (m.DeviceId.HasValue ? $"Invitado {m.DeviceId.Value.ToString("D")[..8]}" : "Desconocido");

            return new AdminHouseMemberDto(
                m.Id,
                appUser?.Id,
                m.UserId,
                m.DeviceId,
                displayName,
                appUser?.Email,
                appUser?.GlobalRole ?? (m.UserId != null ? AppGlobalRoles.User : AppGlobalRoles.Guest),
                m.UserId != null,
                appUser?.IsAdmin ?? false,
                appUser?.Status ?? AppUserStatuses.Active,
                m.Role,
                m.JoinedAt);
        }).ToList();

        var totalProductsCount = await db.Products.CountAsync(p => p.HouseId == houseId, ct);

        var listItems = await db.ShoppingListItems
            .Include(i => i.Product)
            .Include(i => i.ProductType)
            .Where(i => i.HouseId == houseId)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.CreatedAt)
            .Select(i => new AdminSimpleItemDto(
                i.Id,
                i.ProductId,
                i.Product.Name,
                i.ProductType != null ? i.ProductType.Name : null,
                i.ProductType != null ? i.ProductType.FillHex : null,
                i.Format,
                null))
            .ToListAsync(ct);

        var basketItems = await db.BasketItems
            .Include(i => i.Product)
            .Include(i => i.ProductType)
            .Where(i => i.HouseId == houseId)
            .OrderBy(i => i.AddedAt)
            .Select(i => new AdminSimpleItemDto(
                i.Id,
                i.ProductId,
                i.Product.Name,
                i.ProductType != null ? i.ProductType.Name : null,
                i.ProductType != null ? i.ProductType.FillHex : null,
                i.Format,
                i.Quantity))
            .ToListAsync(ct);

        var purchases = await db.Purchases
            .Include(p => p.Store)
            .Include(p => p.Items)
            .Where(p => p.HouseId == houseId)
            .OrderByDescending(p => p.PurchasedAt)
            .ToListAsync(ct);

        var purchaseDtos = purchases.Select(p => new PurchaseDetailDto(
            p.Id,
            p.StoreId,
            p.Store.Name,
            p.PurchasedAt,
            p.CalculatedTotal,
            p.ManualTotal,
            p.ManualTotal ?? p.CalculatedTotal,
            !string.IsNullOrWhiteSpace(p.ReceiptImageUrl),
            p.Items.OrderBy(i => i.ProductNameSnapshot).Select(i => new PurchaseItemDto(
                i.Id,
                i.ProductId,
                i.ProductNameSnapshot,
                i.ProductTypeId,
                i.Format,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal)).ToList())).ToList();

        return new AdminHouseDetailDto(
            house.Id,
            house.Name,
            house.ActiveStore?.Name ?? "Default",
            house.CreatedAt,
            totalProductsCount,
            membersDto,
            listItems,
            basketItems,
            purchaseDtos);
    }

    public async Task<IReadOnlyList<AdminUserSummaryDto>> GetUsersAsync(string? rawBearerToken, CancellationToken ct)
    {
        await userDirectory.RequireAdminAsync(rawBearerToken, ct);
        await BackfillOrphanAppUsersAsync(ct);

        var users = await db.AppUsers
            .OrderByDescending(u => u.IsAdmin)
            .ThenByDescending(u => u.GlobalRole == AppGlobalRoles.User)
            .ThenByDescending(u => u.LastSeenAt)
            .ToListAsync(ct);

        var memberships = await db.HouseMembers.ToListAsync(ct);

        return users.Select(u =>
        {
            var userMemberships = memberships.Where(m =>
                (u.ZitadelUserId != null && m.UserId == u.ZitadelUserId) ||
                (u.DeviceId.HasValue && m.DeviceId == u.DeviceId.Value)).ToList();

            var ownedCount = userMemberships.Count(m => m.Role == HouseRoles.Owner);
            var effectiveRoles = UserDirectoryService.ComputeEffectiveRoles(u, ownedCount > 0);

            return new AdminUserSummaryDto(
                u.Id,
                u.ZitadelUserId,
                u.DeviceId,
                u.DisplayName ?? u.Email ?? u.ZitadelUserId ?? u.DeviceId?.ToString("D") ?? "Usuario",
                u.Email,
                u.Status,
                u.GlobalRole,
                u.IsAdmin,
                u.GlobalRole == AppGlobalRoles.User && !string.IsNullOrWhiteSpace(u.ZitadelUserId),
                effectiveRoles,
                userMemberships.Count,
                ownedCount,
                u.CreatedAt,
                u.LastSeenAt);
        }).ToList();
    }

    public async Task<AdminUserDetailDto> GetUserDetailAsync(Guid appUserId, string? rawBearerToken, CancellationToken ct)
    {
        await userDirectory.RequireAdminAsync(rawBearerToken, ct);
        await BackfillOrphanAppUsersAsync(ct);

        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == appUserId, ct)
                   ?? throw new ResourceNotFoundException("usuario", appUserId);

        return await BuildUserDetailAsync(user, ct);
    }

    public async Task<AdminUserDetailDto> UpdateUserAsync(
        Guid appUserId,
        AdminUpdateUserRequest request,
        string? rawBearerToken,
        CancellationToken ct)
    {
        await userDirectory.RequireAdminAsync(rawBearerToken, ct);

        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == appUserId, ct)
                   ?? throw new ResourceNotFoundException("usuario", appUserId);

        if (!string.Equals(request.Status, AppUserStatuses.Active, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.Status, AppUserStatuses.Blocked, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainValidationException("INVALID_USER_STATUS", "El estado debe ser Active o Blocked.");
        }

        var normalizedStatus = string.Equals(request.Status, AppUserStatuses.Blocked, StringComparison.OrdinalIgnoreCase)
            ? AppUserStatuses.Blocked
            : AppUserStatuses.Active;

        var isGuest = user.GlobalRole == AppGlobalRoles.Guest || string.IsNullOrWhiteSpace(user.ZitadelUserId);

        if (isGuest)
        {
            if (request.IsAdmin)
            {
                throw new DomainValidationException(
                    "GUEST_CANNOT_BE_ADMIN",
                    "Un usuario Guest solo puede tener el rol Guest.");
            }
            if (request.HouseRoles?.Any(h => h.IsOwner) == true)
            {
                throw new DomainValidationException(
                    "GUEST_CANNOT_BE_OWNER",
                    "El rol Owner está reservado para usuarios registrados (User).");
            }
        }

        user.Status = normalizedStatus;
        if (!isGuest)
        {
            user.IsAdmin = request.IsAdmin;
        }

        if (request.DisplayName != null)
        {
            var trimmed = request.DisplayName.Trim();
            if (trimmed.Length > 200)
            {
                throw new DomainValidationException("INVALID_DISPLAY_NAME", "El nombre visible no puede superar 200 caracteres.");
            }
            user.DisplayName = string.IsNullOrWhiteSpace(trimmed) ? user.DisplayName : trimmed;
        }

        if (request.CustomProperties != null)
        {
            var cleanProps = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in request.CustomProperties)
            {
                var k = kv.Key?.Trim();
                if (!string.IsNullOrWhiteSpace(k))
                {
                    cleanProps[k] = kv.Value?.Trim() ?? string.Empty;
                }
            }
            user.CustomPropertiesJson = JsonSerializer.Serialize(cleanProps);
        }

        if (!isGuest && request.HouseRoles != null && request.HouseRoles.Count > 0)
        {
            foreach (var roleChange in request.HouseRoles)
            {
                var targetMembership = await db.HouseMembers.FirstOrDefaultAsync(
                    m => m.HouseId == roleChange.HouseId && m.UserId == user.ZitadelUserId,
                    ct);
                if (targetMembership is null) continue;

                if (roleChange.IsOwner)
                {
                    // Inicialmente solo 1 owner por casa: degradar al owner anterior antes de asignar al nuevo.
                    var existingOwners = await db.HouseMembers
                        .Where(m => m.HouseId == roleChange.HouseId &&
                                    m.Role == HouseRoles.Owner &&
                                    m.Id != targetMembership.Id)
                        .ToListAsync(ct);

                    foreach (var prevOwner in existingOwners)
                    {
                        prevOwner.Role = HouseRoles.Member;
                    }

                    if (existingOwners.Count > 0)
                    {
                        await db.SaveChangesAsync(ct);
                    }

                    targetMembership.Role = HouseRoles.Owner;
                }
                else
                {
                    targetMembership.Role = HouseRoles.Member;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return await BuildUserDetailAsync(user, ct);
    }

    public async Task<IReadOnlyList<AdminCatalogItemDto>> GetItemsAsync(
        string? query,
        string? rawBearerToken,
        CancellationToken ct)
    {
        await userDirectory.RequireAdminAsync(rawBearerToken, ct);

        var products = await db.Products
            .Include(p => p.ProductType)
            .ToListAsync(ct);

        var housesById = await db.Houses
            .ToDictionaryAsync(h => h.Id, h => h.Name, ct);

        var filtered = products.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            filtered = filtered.Where(p =>
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (p.ProductType?.Name.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                (housesById.TryGetValue(p.HouseId, out var houseName) &&
                 houseName.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        var comparer = StringComparer.OrdinalIgnoreCase;

        var grouped = filtered
            .GroupBy(p => new
            {
                NameKey = p.Name.Trim().ToLowerInvariant(),
                TypeKey = (p.ProductType?.Name ?? "Sin tipo").Trim().ToLowerInvariant()
            })
            .Select(g =>
            {
                var first = g.OrderBy(x => x.Name, comparer).First();
                var distinctHouseIds = g.Select(x => x.HouseId).Distinct().ToList();
                var houseCount = distinctHouseIds.Count;
                Guid? singleHouseId = houseCount == 1 ? distinctHouseIds[0] : null;
                var locationLabel = houseCount > 1
                    ? $"{houseCount} casas"
                    : housesById.GetValueOrDefault(distinctHouseIds[0], "Casa");

                return new AdminCatalogItemDto(
                    first.NormalizedName,
                    first.Name.Trim(),
                    first.ProductTypeId,
                    first.ProductType?.Name ?? "Sin tipo",
                    first.ProductType?.FillHex,
                    houseCount,
                    singleHouseId,
                    locationLabel);
            })
            .OrderBy(i => AlphabeticalSortKey(i.Name), comparer)
            .ThenBy(i => i.Name, comparer)
            .ThenBy(i => i.ProductTypeName, comparer)
            .ToList();

        return grouped;
    }

    private static string AlphabeticalSortKey(string text)
    {
        var formD = text.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
        }
        return sb.ToString();
    }

    private async Task<AdminUserDetailDto> BuildUserDetailAsync(AppUser user, CancellationToken ct)
    {
        var memberships = await db.HouseMembers
            .Include(m => m.House)
            .Where(m =>
                (user.ZitadelUserId != null && m.UserId == user.ZitadelUserId) ||
                (user.DeviceId.HasValue && m.DeviceId == user.DeviceId.Value))
            .OrderBy(m => m.JoinedAt)
            .Select(m => new AdminUserHouseMembershipDto(
                m.HouseId,
                m.House.Name,
                m.Role,
                m.JoinedAt))
            .ToListAsync(ct);

        Dictionary<string, string> customProperties;
        try
        {
            customProperties = JsonSerializer.Deserialize<Dictionary<string, string>>(user.CustomPropertiesJson)
                               ?? new Dictionary<string, string>();
        }
        catch
        {
            customProperties = new Dictionary<string, string>();
        }

        var isAuthenticated = user.GlobalRole == AppGlobalRoles.User && !string.IsNullOrWhiteSpace(user.ZitadelUserId);
        var effectiveRoles = UserDirectoryService.ComputeEffectiveRoles(
            user,
            memberships.Any(m => m.HouseRole == HouseRoles.Owner));

        return new AdminUserDetailDto(
            user.Id,
            user.ZitadelUserId,
            user.DeviceId,
            user.DisplayName ?? user.Email ?? user.ZitadelUserId ?? user.DeviceId?.ToString("D") ?? "Usuario",
            user.Email,
            user.Status,
            user.GlobalRole,
            user.IsAdmin,
            isAuthenticated,
            user.AuthMethod,
            effectiveRoles,
            customProperties,
            memberships,
            user.CreatedAt,
            user.LastSeenAt);
    }

    private async Task BackfillOrphanAppUsersAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var members = await db.HouseMembers.ToListAsync(ct);
        var existingZitadelIds = await db.AppUsers
            .Where(u => u.ZitadelUserId != null)
            .Select(u => u.ZitadelUserId!)
            .ToHashSetAsync(ct);
        var existingDeviceIds = await db.AppUsers
            .Where(u => u.DeviceId.HasValue)
            .Select(u => u.DeviceId!.Value)
            .ToHashSetAsync(ct);

        var added = false;
        foreach (var m in members)
        {
            if (m.UserId != null && !existingZitadelIds.Contains(m.UserId))
            {
                db.AppUsers.Add(new AppUser
                {
                    Id = Guid.NewGuid(),
                    ZitadelUserId = m.UserId,
                    GlobalRole = AppGlobalRoles.User,
                    Status = AppUserStatuses.Active,
                    DisplayName = m.UserId,
                    AuthMethod = "zitadel",
                    CustomPropertiesJson = "{}",
                    CreatedAt = m.JoinedAt == default ? now : m.JoinedAt,
                    LastSeenAt = now
                });
                existingZitadelIds.Add(m.UserId);
                added = true;
            }
            else if (m.UserId == null && m.DeviceId.HasValue && !existingDeviceIds.Contains(m.DeviceId.Value))
            {
                db.AppUsers.Add(new AppUser
                {
                    Id = Guid.NewGuid(),
                    DeviceId = m.DeviceId.Value,
                    GlobalRole = AppGlobalRoles.Guest,
                    Status = AppUserStatuses.Active,
                    DisplayName = $"Invitado {m.DeviceId.Value.ToString("D")[..8]}",
                    AuthMethod = "anonymous",
                    CustomPropertiesJson = "{}",
                    CreatedAt = m.JoinedAt == default ? now : m.JoinedAt,
                    LastSeenAt = now
                });
                existingDeviceIds.Add(m.DeviceId.Value);
                added = true;
            }
        }

        if (added)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
