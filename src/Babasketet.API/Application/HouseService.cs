using System.Security.Cryptography;
using System.Text;
using Babasketet.API.Domain;
using Babasketet.API.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Babasketet.API.Application;

public sealed class HouseService(
    BabasketetDbContext db,
    ICallerContextAccessor callerAccessor,
    IDeviceTokenService deviceTokenService,
    IHouseNotifier notifier,
    TimeProvider timeProvider)
{
    public async Task<HousesResponse> GetMyHousesAsync(CancellationToken ct)
    {
        var caller = callerAccessor.Current;
        if (!caller.IsRegistered && !caller.IsAnonymous)
        {
            return new HousesResponse([], DomainCatalogs.AnonymousMaxHouses, false);
        }

        var houses = await QueryAccessibleHouses(caller)
            .OrderBy(h => h.CreatedAt)
            .Select(h => new HouseDto(
                h.Id,
                h.Name,
                h.ActiveStoreId,
                h.ActiveStore != null ? h.ActiveStore.Name : "Default",
                h.CreatedAt))
            .ToListAsync(ct);

        return new HousesResponse(
            houses,
            caller.IsRegistered ? DomainCatalogs.RegisteredMaxHouses : DomainCatalogs.AnonymousMaxHouses,
            caller.IsRegistered);
    }

    public async Task<CreateHouseResponse> CreateHouseAsync(CreateHouseRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            throw new DomainValidationException("INVALID_HOUSE_NAME", "El nombre de la casa debe tener entre 1 y 100 caracteres.");
        }

        var caller = callerAccessor.Current;
        string? issuedToken = null;
        Guid? effectiveDeviceId = caller.DeviceId;

        if (!caller.IsRegistered && !caller.IsAnonymous)
        {
            effectiveDeviceId = Guid.NewGuid();
            issuedToken = deviceTokenService.IssueToken(effectiveDeviceId.Value);
        }
        else
        {
            await EnsureWithinHouseLimitAsync(caller, ct);
        }

        var now = timeProvider.GetUtcNow();
        var house = new House
        {
            Id = Guid.NewGuid(),
            Name = name,
            ActiveStoreId = DomainCatalogs.DefaultStoreId,
            CreatedAt = now,
            UpdatedAt = now
        };

        var member = new HouseMember
        {
            Id = Guid.NewGuid(),
            HouseId = house.Id,
            UserId = caller.IsRegistered ? caller.UserId : null,
            DeviceId = caller.IsRegistered ? null : effectiveDeviceId,
            Role = "owner",
            JoinedAt = now
        };

        db.Houses.Add(house);
        db.HouseMembers.Add(member);
        await db.SaveChangesAsync(ct);

        return new CreateHouseResponse(
            new HouseDto(house.Id, house.Name, house.ActiveStoreId, "Default", house.CreatedAt),
            issuedToken);
    }

    public async Task<InvitationResponse> CreateInvitationAsync(Guid houseId, CancellationToken ct)
    {
        await RequireHouseMembershipAsync(houseId, ct);

        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var token = Base64UrlEncode(rawBytes);
        var hash = HashToken(token);
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(DomainCatalogs.InvitationLifetime);

        db.HouseInvitations.Add(new HouseInvitation
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = expiresAt
        });

        await db.SaveChangesAsync(ct);
        return new InvitationResponse(token, $"/join/{token}", expiresAt);
    }

    public async Task<JoinInvitationResponse> JoinByInvitationAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvitationExpiredException();
        }

        var hash = HashToken(token.Trim());
        var invitation = await db.HouseInvitations
            .Include(i => i.House)
            .ThenInclude(h => h.ActiveStore)
            .FirstOrDefaultAsync(i => i.TokenHash == hash, ct);

        var now = timeProvider.GetUtcNow();
        if (invitation is null || !invitation.IsActiveAt(now))
        {
            throw new InvitationExpiredException();
        }

        var caller = callerAccessor.Current;
        string? issuedToken = null;
        Guid? effectiveDeviceId = caller.DeviceId;

        if (!caller.IsRegistered && !caller.IsAnonymous)
        {
            effectiveDeviceId = Guid.NewGuid();
            issuedToken = deviceTokenService.IssueToken(effectiveDeviceId.Value);
        }

        var alreadyMember = await db.HouseMembers.AnyAsync(m =>
            m.HouseId == invitation.HouseId &&
            ((caller.UserId != null && m.UserId == caller.UserId) ||
             (effectiveDeviceId != null && m.DeviceId == effectiveDeviceId)), ct);

        if (!alreadyMember)
        {
            if (caller.IsRegistered || caller.IsAnonymous)
            {
                await EnsureWithinHouseLimitAsync(caller, ct);
            }

            db.HouseMembers.Add(new HouseMember
            {
                Id = Guid.NewGuid(),
                HouseId = invitation.HouseId,
                UserId = caller.IsRegistered ? caller.UserId : null,
                DeviceId = caller.IsRegistered ? null : effectiveDeviceId,
                Role = "member",
                JoinedAt = now
            });

            await db.SaveChangesAsync(ct);
        }

        var h = invitation.House;
        return new JoinInvitationResponse(
            new HouseDto(h.Id, h.Name, h.ActiveStoreId, h.ActiveStore?.Name ?? "Default", h.CreatedAt),
            issuedToken);
    }

    public async Task<HousesResponse> ClaimAnonymousHousesAsync(ClaimHouseRequest request, CancellationToken ct)
    {
        var caller = callerAccessor.Current;
        if (!caller.IsRegistered || string.IsNullOrWhiteSpace(caller.UserId))
        {
            throw new ForbiddenAccessException("Debes iniciar sesión para vincular casas a tu cuenta.");
        }

        if (string.IsNullOrWhiteSpace(request.DeviceToken))
        {
            return await GetMyHousesAsync(ct);
        }

        var deviceId = deviceTokenService.TryValidateToken(request.DeviceToken);
        if (!deviceId.HasValue)
        {
            throw new DomainValidationException("INVALID_DEVICE_TOKEN", "El token de dispositivo no es válido.");
        }

        var deviceMemberships = await db.HouseMembers
            .Where(m => m.DeviceId == deviceId.Value)
            .ToListAsync(ct);

        var currentUserHouseIds = await db.HouseMembers
            .Where(m => m.UserId == caller.UserId)
            .Select(m => m.HouseId)
            .ToHashSetAsync(ct);

        foreach (var membership in deviceMemberships)
        {
            if (currentUserHouseIds.Contains(membership.HouseId))
            {
                db.HouseMembers.Remove(membership);
                continue;
            }

            if (currentUserHouseIds.Count >= DomainCatalogs.RegisteredMaxHouses)
            {
                throw new HouseLimitExceededException(DomainCatalogs.RegisteredMaxHouses, false);
            }

            membership.UserId = caller.UserId;
            membership.DeviceId = null;
            currentUserHouseIds.Add(membership.HouseId);
        }

        await db.SaveChangesAsync(ct);
        return await GetMyHousesAsync(ct);
    }

    public async Task<IReadOnlyList<StoreDto>> GetStoresAsync(Guid houseId, CancellationToken ct)
    {
        await RequireHouseMembershipAsync(houseId, ct);

        return await db.Stores
            .Where(s => s.HouseId == null || s.HouseId == houseId)
            .OrderByDescending(s => s.IsDefault)
            .ThenByDescending(s => s.IsSystem)
            .ThenBy(s => s.Name)
            .Select(s => new StoreDto(s.Id, s.Name, s.IconKey, s.IsDefault, s.IsSystem))
            .ToListAsync(ct);
    }

    public async Task<StoreDto> CreateStoreAsync(Guid houseId, CreateStoreRequest request, CancellationToken ct)
    {
        await RequireHouseMembershipAsync(houseId, ct);

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
        {
            throw new DomainValidationException("INVALID_STORE_NAME", "El nombre del supermercado debe tener entre 1 y 80 caracteres.");
        }

        var exists = await db.Stores.AnyAsync(s =>
            (s.HouseId == null || s.HouseId == houseId) &&
            s.Name.ToLower() == name.ToLower(), ct);
        if (exists)
        {
            throw new DomainValidationException("DUPLICATE_STORE", $"El supermercado '{name}' ya existe.");
        }

        var store = new Store
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            Name = name,
            IconKey = "custom_store",
            IsDefault = false,
            IsSystem = false
        };

        db.Stores.Add(store);
        await db.SaveChangesAsync(ct);
        await notifier.NotifyCatalogUpdatedAsync(houseId, "stores", ct);

        return new StoreDto(store.Id, store.Name, store.IconKey, store.IsDefault, store.IsSystem);
    }

    public async Task SetActiveStoreAsync(Guid houseId, SetActiveStoreRequest request, CancellationToken ct)
    {
        var house = await RequireHouseMembershipAsync(houseId, ct);

        var storeExists = await db.Stores.AnyAsync(s =>
            s.Id == request.StoreId && (s.HouseId == null || s.HouseId == houseId), ct);
        if (!storeExists)
        {
            throw new ResourceNotFoundException("supermercado", request.StoreId);
        }

        house.ActiveStoreId = request.StoreId;
        house.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        await notifier.NotifyActiveStoreChangedAsync(houseId, request.StoreId, ct);
        await notifier.NotifyBasketUpdatedAsync(houseId, ct);
        await notifier.NotifyListUpdatedAsync(houseId, ct);
    }

    public async Task<IReadOnlyList<ProductTypeDto>> GetProductTypesAsync(Guid houseId, CancellationToken ct)
    {
        await RequireHouseMembershipAsync(houseId, ct);

        return await db.ProductTypes
            .Where(t => t.HouseId == null || t.HouseId == houseId)
            .OrderByDescending(t => t.IsSystem)
            .ThenBy(t => t.Name)
            .Select(t => new ProductTypeDto(t.Id, t.Name, t.FillHex, t.BorderHex, t.IsSystem))
            .ToListAsync(ct);
    }

    public async Task<ProductTypeDto> CreateProductTypeAsync(Guid houseId, CreateProductTypeRequest request, CancellationToken ct)
    {
        await RequireHouseMembershipAsync(houseId, ct);

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
        {
            throw new DomainValidationException("INVALID_TYPE_NAME", "El nombre del tipo debe tener entre 1 y 80 caracteres.");
        }

        if (!IsValidHexColor(request.FillHex))
        {
            throw new DomainValidationException("INVALID_COLOR", "El color de fondo debe ser un hexadecimal #RRGGBB válido.");
        }

        if (!string.IsNullOrWhiteSpace(request.BorderHex) && !IsValidHexColor(request.BorderHex))
        {
            throw new DomainValidationException("INVALID_BORDER_COLOR", "El color de borde debe ser un hexadecimal #RRGGBB válido.");
        }

        var type = new ProductType
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            Name = name,
            FillHex = request.FillHex.ToUpperInvariant(),
            BorderHex = string.IsNullOrWhiteSpace(request.BorderHex) ? null : request.BorderHex.ToUpperInvariant(),
            IsSystem = false
        };

        db.ProductTypes.Add(type);
        await db.SaveChangesAsync(ct);
        await notifier.NotifyCatalogUpdatedAsync(houseId, "product-types", ct);

        return new ProductTypeDto(type.Id, type.Name, type.FillHex, type.BorderHex, type.IsSystem);
    }

    public async Task<House> RequireHouseMembershipAsync(Guid houseId, CancellationToken ct)
    {
        var caller = callerAccessor.Current;
        if (!caller.IsRegistered && !caller.IsAnonymous)
        {
            throw new ForbiddenAccessException();
        }

        var house = await db.Houses
            .Include(h => h.ActiveStore)
            .FirstOrDefaultAsync(h => h.Id == houseId, ct)
            ?? throw new ResourceNotFoundException("casa", houseId);

        var isMember = await db.HouseMembers.AnyAsync(m =>
            m.HouseId == houseId &&
            ((caller.UserId != null && m.UserId == caller.UserId) ||
             (caller.DeviceId != null && m.DeviceId == caller.DeviceId)), ct);

        if (!isMember)
        {
            throw new ForbiddenAccessException();
        }

        return house;
    }

    private async Task EnsureWithinHouseLimitAsync(CallerIdentity caller, CancellationToken ct)
    {
        var max = caller.IsRegistered ? DomainCatalogs.RegisteredMaxHouses : DomainCatalogs.AnonymousMaxHouses;
        var currentCount = await QueryAccessibleHouses(caller).CountAsync(ct);

        if (currentCount >= max)
        {
            throw new HouseLimitExceededException(max, !caller.IsRegistered);
        }
    }

    private IQueryable<House> QueryAccessibleHouses(CallerIdentity caller)
    {
        if (caller.IsRegistered && caller.UserId != null)
        {
            return db.HouseMembers
                .Where(m => m.UserId == caller.UserId)
                .Select(m => m.House);
        }

        if (caller.DeviceId.HasValue)
        {
            return db.HouseMembers
                .Where(m => m.DeviceId == caller.DeviceId.Value)
                .Select(m => m.House);
        }

        return db.Houses.Where(_ => false);
    }

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool IsValidHexColor(string? hex) =>
        !string.IsNullOrWhiteSpace(hex) &&
        hex.Length == 7 &&
        hex[0] == '#' &&
        hex.Skip(1).All(Uri.IsHexDigit);
}
