namespace Babasketet.API.Domain;

public static class DomainCatalogs
{
    public const int AnonymousMaxHouses = 1;
    public const int RegisteredMaxHouses = 4;
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromHours(1);

    public static readonly Guid DefaultStoreId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    public static readonly IReadOnlyList<string> AllowedFormats =
    [
        "Ud",
        "Docena",
        "Kg",
        "g",
        "L",
        "Cl",
        "Pack-2",
        "Pack-3",
        "Pack-4",
        "Pack-6",
        "Pack-8"
    ];

    public static readonly HashSet<string> AllowedFormatsSet =
        new(AllowedFormats, StringComparer.Ordinal);

    public sealed record StoreSeed(Guid Id, string Name, string IconKey, bool IsDefault);

    public static readonly IReadOnlyList<StoreSeed> DefaultStores =
    [
        new(DefaultStoreId, "Default", "store_default", true),
        new(Guid.Parse("10000000-0000-0000-0000-000000000002"), "Mercadona", "mercadona", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000003"), "Carrefour", "carrefour", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000004"), "Dia", "dia", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000005"), "Lidl", "lidl", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000006"), "Aldi", "aldi", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000007"), "Tienda de Barrio", "local_shop", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000008"), "Spar", "spar", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000009"), "Condis", "condis", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000010"), "El Jamón", "el_jamon", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000011"), "Eroski", "eroski", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000012"), "Consum", "consum", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000013"), "Caprabo", "caprabo", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000014"), "Costco", "costco", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000015"), "Supercor", "supercor", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000016"), "Hipercor", "hipercor", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000017"), "Alcampo/Auchan", "alcampo", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000018"), "Supeco", "supeco", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000019"), "Intermarché", "intermarche", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000020"), "Continente", "continente", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000021"), "Coviran", "coviran", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000022"), "Pingo Doce", "pingo_doce", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000023"), "Alsara", "alsara", false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000024"), "Comarket", "comarket", false),
    ];

    public sealed record ProductTypeSeed(Guid Id, string Name, string FillHex, string? BorderHex);

    public static readonly IReadOnlyList<ProductTypeSeed> DefaultProductTypes =
    [
        new(Guid.Parse("20000000-0000-0000-0000-000000000001"), "Fruta/Verdura", "#86EFAC", null),
        new(Guid.Parse("20000000-0000-0000-0000-000000000002"), "Carne", "#EF4444", null),
        new(Guid.Parse("20000000-0000-0000-0000-000000000003"), "Charcuteria/Queso", "#F472B6", null),
        new(Guid.Parse("20000000-0000-0000-0000-000000000004"), "Pescado", "#7DD3FC", null),
        new(Guid.Parse("20000000-0000-0000-0000-000000000005"), "Baño", "#2563EB", null),
        new(Guid.Parse("20000000-0000-0000-0000-000000000006"), "Hogar/Limpieza", "#166534", null),
        new(Guid.Parse("20000000-0000-0000-0000-000000000007"), "Conservas/Sopas/Cremas", "#FDBA74", null),
        new(Guid.Parse("20000000-0000-0000-0000-000000000008"), "Aceite/Salsas/Especias", "#808000", "#000000"),
        new(Guid.Parse("20000000-0000-0000-0000-000000000009"), "Agua/Refrescos", "#7DD3FC", "#78350F"),
        new(Guid.Parse("20000000-0000-0000-0000-000000000010"), "Arroz/Legumbres/Pasta", "#5C3A21", null),
        new(Guid.Parse("20000000-0000-0000-0000-000000000011"), "Dulces/Azucarados/Postres", "#C4A484", "#5C3A21"),
        new(Guid.Parse("20000000-0000-0000-0000-000000000012"), "Lacteos", "#FFFFFF", "#FF00FF"),
        new(Guid.Parse("20000000-0000-0000-0000-000000000013"), "Alcohol", "#FACC15", "#C4A484"),
        new(Guid.Parse("20000000-0000-0000-0000-000000000014"), "Desayuno", "#C4A484", "#2563EB"),
        new(Guid.Parse("20000000-0000-0000-0000-000000000015"), "Congelado", "#1E3A8A", "#7DD3FC"),
        new(Guid.Parse("20000000-0000-0000-0000-000000000016"), "Droguería", "#C084FC", null),
        new(Guid.Parse("20000000-0000-0000-0000-000000000017"), "Mascotas", "#F5F5DC", "#000000"),
        new(Guid.Parse("20000000-0000-0000-0000-000000000018"), "Panaderia", "#C4A484", null),
    ];

    public static string NormalizeProductName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var parts = name.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts).ToLowerInvariant();
    }
}
