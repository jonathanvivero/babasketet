using Babasketet.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace Babasketet.API.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(BabasketetDbContext db, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);
        await SeedCatalogsAsync(db, ct);
    }

    public static async Task SeedCatalogsAsync(BabasketetDbContext db, CancellationToken ct = default)
    {
        var existingStoreIds = await db.Stores
            .Where(s => s.IsSystem)
            .Select(s => s.Id)
            .ToHashSetAsync(ct);

        foreach (var seed in DomainCatalogs.DefaultStores)
        {
            if (!existingStoreIds.Contains(seed.Id))
            {
                db.Stores.Add(new Store
                {
                    Id = seed.Id,
                    HouseId = null,
                    Name = seed.Name,
                    IconKey = seed.IconKey,
                    IsDefault = seed.IsDefault,
                    IsSystem = true
                });
            }
        }

        var existingTypeIds = await db.ProductTypes
            .Where(t => t.IsSystem)
            .Select(t => t.Id)
            .ToHashSetAsync(ct);

        foreach (var seed in DomainCatalogs.DefaultProductTypes)
        {
            if (!existingTypeIds.Contains(seed.Id))
            {
                db.ProductTypes.Add(new ProductType
                {
                    Id = seed.Id,
                    HouseId = null,
                    Name = seed.Name,
                    FillHex = seed.FillHex,
                    BorderHex = seed.BorderHex,
                    IsSystem = true
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
