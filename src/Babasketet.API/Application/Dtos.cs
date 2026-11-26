namespace Babasketet.API.Application;

public sealed record CallerIdentity(
    string? UserId,
    Guid? DeviceId,
    bool IsPremium,
    string? Email = null,
    string? DisplayName = null,
    IReadOnlyCollection<string>? Roles = null)
{
    public bool IsRegistered => !string.IsNullOrWhiteSpace(UserId);
    public bool IsAnonymous => !IsRegistered && DeviceId.HasValue;
    public bool HasRole(string role) =>
        Roles?.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase)) == true;
}

public interface ICallerContextAccessor
{
    CallerIdentity Current { get; }
}

public interface IDeviceTokenService
{
    string IssueToken(Guid deviceId);
    Guid? TryValidateToken(string token);
}

public interface IHouseNotifier
{
    Task NotifyListUpdatedAsync(Guid houseId, CancellationToken ct = default);
    Task NotifyBasketUpdatedAsync(Guid houseId, CancellationToken ct = default);
    Task NotifyPurchaseCompletedAsync(Guid houseId, Guid purchaseId, CancellationToken ct = default);
    Task NotifyActiveStoreChangedAsync(Guid houseId, Guid storeId, CancellationToken ct = default);
    Task NotifyCatalogUpdatedAsync(Guid houseId, string catalogType, CancellationToken ct = default);
}

public sealed record HouseDto(
    Guid Id,
    string Name,
    Guid ActiveStoreId,
    string ActiveStoreName,
    DateTimeOffset CreatedAt);

public sealed record HousesResponse(
    IReadOnlyList<HouseDto> Houses,
    int MaxHouses,
    bool IsRegistered);

public sealed record CreateHouseRequest(string Name);

public sealed record CreateHouseResponse(
    HouseDto House,
    string? DeviceToken);

public sealed record InvitationResponse(
    string Token,
    string Path,
    DateTimeOffset ExpiresAt);

public sealed record JoinInvitationResponse(
    HouseDto House,
    string? DeviceToken);

public sealed record ClaimHouseRequest(string? DeviceToken);

public sealed record StoreDto(
    Guid Id,
    string Name,
    string IconKey,
    bool IsDefault,
    bool IsSystem);

public sealed record CreateStoreRequest(string Name);
public sealed record SetActiveStoreRequest(Guid StoreId);

public sealed record ProductTypeDto(
    Guid Id,
    string Name,
    string FillHex,
    string? BorderHex,
    bool IsSystem);

public sealed record CreateProductTypeRequest(string Name, string FillHex, string? BorderHex);

public sealed record ProductSuggestionDto(
    Guid Id,
    string Name,
    Guid? ProductTypeId,
    ProductTypeDto? ProductType,
    string DefaultFormat,
    decimal? CurrentPrice);

public sealed record ShoppingListItemDto(
    Guid Id,
    Guid ProductId,
    string Name,
    Guid? ProductTypeId,
    ProductTypeDto? ProductType,
    string Format,
    decimal? CurrentPrice,
    int SortOrder);

public sealed record AddListItemRequest(
    Guid? ProductId,
    string? Name,
    Guid? ProductTypeId,
    string? Format);

public sealed record UpdateListItemRequest(
    string? Name,
    Guid? ProductTypeId,
    bool ClearProductType,
    string? Format);

public sealed record ReorderListRequest(IReadOnlyList<Guid> OrderedItemIds);

public sealed record BasketItemDto(
    Guid Id,
    Guid ProductId,
    string Name,
    Guid? ProductTypeId,
    ProductTypeDto? ProductType,
    string Format,
    decimal Quantity,
    decimal? CurrentPrice,
    decimal LineTotal);

public sealed record BasketResponse(
    Guid ActiveStoreId,
    string ActiveStoreName,
    IReadOnlyList<BasketItemDto> Items,
    decimal CalculatedTotal);

public sealed record UpdateBasketItemRequest(
    decimal? Quantity,
    Guid? ProductTypeId,
    bool ClearProductType,
    string? Format,
    string? Name = null);

public sealed record SetPriceRequest(decimal Price);

public sealed record PriceHistoryEntryDto(
    Guid ProductId,
    Guid StoreId,
    decimal Price,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo);

public sealed record PurchaseSummaryDto(
    Guid Id,
    Guid StoreId,
    string StoreName,
    DateTimeOffset PurchasedAt,
    decimal CalculatedTotal,
    decimal? ManualTotal,
    decimal EffectiveTotal,
    int ItemCount,
    bool HasReceipt);

public sealed record PurchaseItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid? ProductTypeId,
    string Format,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record PurchaseDetailDto(
    Guid Id,
    Guid StoreId,
    string StoreName,
    DateTimeOffset PurchasedAt,
    decimal CalculatedTotal,
    decimal? ManualTotal,
    decimal EffectiveTotal,
    bool HasReceipt,
    IReadOnlyList<PurchaseItemDto> Items);

public sealed record UpdateManualTotalRequest(decimal ManualTotal);

// === Perfil del llamante y Panel Admin (/admin-bo) ===
public sealed record MeHouseRoleDto(
    Guid HouseId,
    string HouseName,
    string HouseRole);

public sealed record MeResponse(
    Guid? AppUserId,
    string? ZitadelUserId,
    Guid? DeviceId,
    string GlobalRole,
    bool IsAuthenticated,
    bool IsAdmin,
    string Status,
    string? Email,
    string? DisplayName,
    IReadOnlyList<string> EffectiveRoles,
    IReadOnlyList<MeHouseRoleDto> Houses);

public sealed record AdminHouseSummaryDto(
    Guid Id,
    string Name,
    string ActiveStoreName,
    int MemberCount,
    int TotalProductsCount,
    int ListItemsCount,
    int BasketItemsCount,
    int PurchasesCount,
    DateTimeOffset CreatedAt);

public sealed record AdminHouseMemberDto(
    Guid MembershipId,
    Guid? AppUserId,
    string? ZitadelUserId,
    Guid? DeviceId,
    string DisplayName,
    string? Email,
    string GlobalRole,
    bool IsAuthenticated,
    bool IsAdmin,
    string UserStatus,
    string HouseRole,
    DateTimeOffset JoinedAt);

public sealed record AdminSimpleItemDto(
    Guid Id,
    Guid ProductId,
    string Name,
    string? ProductTypeName,
    string? ProductTypeFillHex,
    string Format,
    decimal? Quantity);

public sealed record AdminHouseDetailDto(
    Guid Id,
    string Name,
    string ActiveStoreName,
    DateTimeOffset CreatedAt,
    int TotalProductsCount,
    IReadOnlyList<AdminHouseMemberDto> Members,
    IReadOnlyList<AdminSimpleItemDto> ListItems,
    IReadOnlyList<AdminSimpleItemDto> BasketItems,
    IReadOnlyList<PurchaseDetailDto> Purchases);

public sealed record AdminUserHouseMembershipDto(
    Guid HouseId,
    string HouseName,
    string HouseRole,
    DateTimeOffset JoinedAt);

public sealed record AdminUserSummaryDto(
    Guid Id,
    string? ZitadelUserId,
    Guid? DeviceId,
    string DisplayName,
    string? Email,
    string Status,
    string GlobalRole,
    bool IsAdmin,
    bool IsAuthenticated,
    IReadOnlyList<string> EffectiveRoles,
    int HousesCount,
    int OwnedHousesCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt);

public sealed record AdminUserDetailDto(
    Guid Id,
    string? ZitadelUserId,
    Guid? DeviceId,
    string DisplayName,
    string? Email,
    string Status,
    string GlobalRole,
    bool IsAdmin,
    bool IsAuthenticated,
    string AuthMethod,
    IReadOnlyList<string> EffectiveRoles,
    IReadOnlyDictionary<string, string> CustomProperties,
    IReadOnlyList<AdminUserHouseMembershipDto> Houses,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt);

public sealed record AdminUserHouseRoleUpdate(
    Guid HouseId,
    bool IsOwner);

public sealed record AdminUpdateUserRequest(
    string Status,
    bool IsAdmin,
    string? DisplayName,
    IReadOnlyDictionary<string, string>? CustomProperties,
    IReadOnlyList<AdminUserHouseRoleUpdate>? HouseRoles);

public sealed record AdminCatalogItemDto(
    string NormalizedName,
    string Name,
    Guid? ProductTypeId,
    string ProductTypeName,
    string? ProductTypeFillHex,
    int HouseCount,
    Guid? SingleHouseId,
    string LocationLabel);

// === Configuracion de Correo Saliente (SMTP) ===
public sealed record EmailConfigDto(
    bool IsConfigured,
    string Host,
    int Port,
    string? Username,
    bool HasPassword,
    string SecurityMode,
    string FromEmail,
    string FromName,
    bool Enabled,
    DateTimeOffset? UpdatedAt);

public sealed record SaveEmailConfigRequest(
    string Host,
    int Port,
    string? Username,
    string? Password,
    bool ClearPassword,
    string SecurityMode,
    string FromEmail,
    string? FromName,
    bool Enabled);

public sealed record TestEmailRequest(
    string? ToEmail,
    SaveEmailConfigRequest? OverrideConfig = null);

public sealed record TestEmailResultDto(
    bool Success,
    string Recipient,
    string Message,
    DateTimeOffset SentAt);

// === Sistema de Notificaciones Admin ===
public sealed record AdminNotificationDto(
    Guid Id,
    string Title,
    string Body,
    string Category,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    DateTimeOffset? ArchivedAt);

public sealed record AdminNotificationsPageDto(
    IReadOnlyList<AdminNotificationDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    int NewCount);

public sealed record CreateAdminNotificationRequest(
    string Title,
    string Body,
    string? Category = "system");

public sealed record UpdateNotificationStatusRequest(string Status);
