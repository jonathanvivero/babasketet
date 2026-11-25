namespace Babasketet.API.Application;

public sealed record CallerIdentity(
    string? UserId,
    Guid? DeviceId,
    bool IsPremium)
{
    public bool IsRegistered => !string.IsNullOrWhiteSpace(UserId);
    public bool IsAnonymous => !IsRegistered && DeviceId.HasValue;
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
    string? Format);

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
