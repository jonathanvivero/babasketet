namespace Babasketet.API.Domain;

public static class AppGlobalRoles
{
    public const string Guest = "Guest";
    public const string User = "User";
}

public static class AppUserStatuses
{
    public const string Active = "Active";
    public const string Blocked = "Blocked";
}

public static class HouseRoles
{
    public const string Owner = "owner";
    public const string Member = "member";
}

public class AppUser
{
    public Guid Id { get; set; }
    public string? ZitadelUserId { get; set; }
    public Guid? DeviceId { get; set; }
    public string GlobalRole { get; set; } = AppGlobalRoles.Guest;
    public bool IsAdmin { get; set; }
    public string Status { get; set; } = AppUserStatuses.Active;
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string AuthMethod { get; set; } = "anonymous";
    public string CustomPropertiesJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}

public class House
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ActiveStoreId { get; set; } = DomainCatalogs.DefaultStoreId;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Store? ActiveStore { get; set; }
    public ICollection<HouseMember> Members { get; set; } = [];
}

public class HouseMember
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public string? UserId { get; set; }
    public Guid? DeviceId { get; set; }
    public string Role { get; set; } = "member";
    public DateTimeOffset JoinedAt { get; set; }

    public House House { get; set; } = null!;
}

public class HouseInvitation
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public House House { get; set; } = null!;

    public bool IsActiveAt(DateTimeOffset now) =>
        RevokedAt is null && ExpiresAt > now;
}

public class Store
{
    public Guid Id { get; set; }
    public Guid? HouseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IconKey { get; set; } = "custom_store";
    public bool IsDefault { get; set; }
    public bool IsSystem { get; set; }
}

public class ProductType
{
    public Guid Id { get; set; }
    public Guid? HouseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FillHex { get; set; } = "#000000";
    public string? BorderHex { get; set; }
    public bool IsSystem { get; set; }
}

public class Product
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public Guid? ProductTypeId { get; set; }
    public string DefaultFormat { get; set; } = "Ud";
    public DateTimeOffset LastUsedAt { get; set; }

    public ProductType? ProductType { get; set; }
}

public class ShoppingListItem
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductTypeId { get; set; }
    public string Format { get; set; } = "Ud";
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Product Product { get; set; } = null!;
    public ProductType? ProductType { get; set; }
}

public class BasketItem
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductTypeId { get; set; }
    public string Format { get; set; } = "Ud";
    public decimal Quantity { get; set; } = 1m;
    public DateTimeOffset AddedAt { get; set; }

    public Product Product { get; set; } = null!;
    public ProductType? ProductType { get; set; }
}

public class ProductPriceHistory
{
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public decimal Price { get; set; }

    public Product Product { get; set; } = null!;
    public Store Store { get; set; } = null!;
}

public class Purchase
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public Guid StoreId { get; set; }
    public DateTimeOffset PurchasedAt { get; set; }
    public decimal CalculatedTotal { get; set; }
    public decimal? ManualTotal { get; set; }
    public string? ReceiptImageUrl { get; set; }

    public Store Store { get; set; } = null!;
    public ICollection<PurchaseItem> Items { get; set; } = [];
}

public class PurchaseItem
{
    public Guid Id { get; set; }
    public Guid PurchaseId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public Guid? ProductTypeId { get; set; }
    public string Format { get; set; } = "Ud";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public Purchase Purchase { get; set; } = null!;
}
