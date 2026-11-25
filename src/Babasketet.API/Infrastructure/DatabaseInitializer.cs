using Babasketet.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace Babasketet.API.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(BabasketetDbContext db, CancellationToken ct = default)
    {
        // Si la BD se creó previamente con EnsureCreated sin __EFMigrationsHistory,
        // registramos la migración baseline antes de aplicar las nuevas migraciones incrementales.
        await EnsureBaselineIfPreExistingSchemaAsync(db, ct);
        await db.Database.MigrateAsync(ct);
        await SeedCatalogsAsync(db, ct);
    }

    private static async Task EnsureBaselineIfPreExistingSchemaAsync(BabasketetDbContext db, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        var shouldClose = conn.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await conn.OpenAsync(ct);
        }

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT
                    to_regclass('public.houses') IS NOT NULL,
                    to_regclass('public."__EFMigrationsHistory"') IS NOT NULL;
                """;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return;
            var hasHouses = !reader.IsDBNull(0) && reader.GetBoolean(0);
            var hasHistory = !reader.IsDBNull(1) && reader.GetBoolean(1);
            await reader.DisposeAsync();

            if (!hasHouses) return;

            var baselineNeeded = !hasHistory;
            if (hasHistory)
            {
                await using var countCmd = conn.CreateCommand();
                countCmd.CommandText = """SELECT COUNT(*) FROM "__EFMigrationsHistory";""";
                var existingCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync(ct));
                baselineNeeded = existingCount == 0;
            }

            if (baselineNeeded)
            {
                await using var seedCmd = conn.CreateCommand();
                seedCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                        "MigrationId" character varying(150) NOT NULL,
                        "ProductVersion" character varying(32) NOT NULL,
                        CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                    );
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    VALUES ('20261125000001_InitialBaseline', '10.0.0')
                    ON CONFLICT DO NOTHING;
                    """;
                await seedCmd.ExecuteNonQueryAsync(ct);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await conn.CloseAsync();
            }
        }
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
