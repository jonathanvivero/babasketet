using Babasketet.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace Babasketet.API.Infrastructure;

public sealed class BabasketetDbContext(DbContextOptions<BabasketetDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<SystemEmailConfig> SystemEmailConfigs => Set<SystemEmailConfig>();
    public DbSet<AdminNotification> AdminNotifications => Set<AdminNotification>();
    public DbSet<PlatformInvitation> PlatformInvitations => Set<PlatformInvitation>();
    public DbSet<House> Houses => Set<House>();
    public DbSet<HouseMember> HouseMembers => Set<HouseMember>();
    public DbSet<HouseInvitation> HouseInvitations => Set<HouseInvitation>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();
    public DbSet<BasketItem> BasketItems => Set<BasketItem>();
    public DbSet<ProductPriceHistory> ProductPriceHistories => Set<ProductPriceHistory>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("app_users", t =>
            {
                t.HasCheckConstraint("ck_app_users_identity", "zitadel_user_id IS NOT NULL OR device_id IS NOT NULL");
                t.HasCheckConstraint(
                    "ck_app_users_global_role",
                    "(global_role = 'Guest' AND zitadel_user_id IS NULL AND is_admin = FALSE) OR (global_role = 'User' AND zitadel_user_id IS NOT NULL)");
                t.HasCheckConstraint("ck_app_users_status", "status IN ('Active', 'Blocked')");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ZitadelUserId).HasColumnName("zitadel_user_id").HasMaxLength(128);
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.GlobalRole).HasColumnName("global_role").HasMaxLength(32).IsRequired();
            e.Property(x => x.IsAdmin).HasColumnName("is_admin").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(256);
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200);
            e.Property(x => x.AuthMethod).HasColumnName("auth_method").HasMaxLength(32).IsRequired();
            e.Property(x => x.CustomPropertiesJson).HasColumnName("custom_properties_json").HasColumnType("text").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.Property(x => x.LastSeenAt).HasColumnName("last_seen_at").IsRequired();
            e.HasIndex(x => x.ZitadelUserId)
                .IsUnique()
                .HasFilter("zitadel_user_id IS NOT NULL");
            e.HasIndex(x => x.DeviceId)
                .IsUnique()
                .HasFilter("device_id IS NOT NULL");
        });

        modelBuilder.Entity<SystemEmailConfig>(e =>
        {
            e.ToTable("system_email_configs", t =>
                t.HasCheckConstraint("ck_system_email_configs_port", "port BETWEEN 1 AND 65535"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Host).HasColumnName("host").HasMaxLength(255).IsRequired();
            e.Property(x => x.Port).HasColumnName("port").IsRequired();
            e.Property(x => x.Username).HasColumnName("username").HasMaxLength(255);
            e.Property(x => x.EncryptedPassword).HasColumnName("encrypted_password").HasColumnType("text");
            e.Property(x => x.SecurityMode).HasColumnName("security_mode").HasMaxLength(32).IsRequired();
            e.Property(x => x.FromEmail).HasColumnName("from_email").HasMaxLength(256).IsRequired();
            e.Property(x => x.FromName).HasColumnName("from_name").HasMaxLength(150).IsRequired();
            e.Property(x => x.Enabled).HasColumnName("enabled").IsRequired();
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        });

        modelBuilder.Entity<AdminNotification>(e =>
        {
            e.ToTable("admin_notifications", t =>
                t.HasCheckConstraint("ck_admin_notifications_status", "status IN ('New', 'Read', 'Archived')"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            e.Property(x => x.Body).HasColumnName("body").HasColumnType("text").IsRequired();
            e.Property(x => x.Category).HasColumnName("category").HasMaxLength(64).IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.Property(x => x.ReadAt).HasColumnName("read_at");
            e.Property(x => x.ArchivedAt).HasColumnName("archived_at");
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        modelBuilder.Entity<House>(e =>
        {
            e.ToTable("houses");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            e.Property(x => x.ActiveStoreId).HasColumnName("active_store_id").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            e.HasOne(x => x.ActiveStore)
                .WithMany()
                .HasForeignKey(x => x.ActiveStoreId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HouseMember>(e =>
        {
            e.ToTable("house_members", t =>
            {
                t.HasCheckConstraint("ck_house_members_identity", "user_id IS NOT NULL OR device_id IS NOT NULL");
                t.HasCheckConstraint("ck_house_members_owner_registered", "role <> 'owner' OR user_id IS NOT NULL");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.HouseId).HasColumnName("house_id").IsRequired();
            e.Property(x => x.UserId).HasColumnName("user_id").HasMaxLength(128);
            e.Property(x => x.DeviceId).HasColumnName("device_id");
            e.Property(x => x.Role).HasColumnName("role").HasMaxLength(32).IsRequired();
            e.Property(x => x.JoinedAt).HasColumnName("joined_at").IsRequired();
            e.HasOne(x => x.House)
                .WithMany(h => h.Members)
                .HasForeignKey(x => x.HouseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.HouseId, x.UserId })
                .IsUnique()
                .HasFilter("user_id IS NOT NULL");
            e.HasIndex(x => new { x.HouseId, x.DeviceId })
                .IsUnique()
                .HasFilter("device_id IS NOT NULL");
            e.HasIndex(x => x.HouseId)
                .HasDatabaseName("ux_house_members_single_owner")
                .IsUnique()
                .HasFilter("role = 'owner' AND user_id IS NOT NULL");
        });

        modelBuilder.Entity<HouseInvitation>(e =>
        {
            e.ToTable("house_invitations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.HouseId).HasColumnName("house_id").IsRequired();
            e.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.House)
                .WithMany()
                .HasForeignKey(x => x.HouseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Store>(e =>
        {
            e.ToTable("stores");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.HouseId).HasColumnName("house_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
            e.Property(x => x.IconKey).HasColumnName("icon_key").HasMaxLength(64).IsRequired();
            e.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();
            e.Property(x => x.IsSystem).HasColumnName("is_system").IsRequired();
            e.HasIndex(x => new { x.HouseId, x.Name });
        });

        modelBuilder.Entity<ProductType>(e =>
        {
            e.ToTable("product_types");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.HouseId).HasColumnName("house_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
            e.Property(x => x.FillHex).HasColumnName("fill_hex").HasMaxLength(7).IsRequired();
            e.Property(x => x.BorderHex).HasColumnName("border_hex").HasMaxLength(7);
            e.Property(x => x.IsSystem).HasColumnName("is_system").IsRequired();
            e.HasIndex(x => new { x.HouseId, x.Name });
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.HouseId).HasColumnName("house_id").IsRequired();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            e.Property(x => x.NormalizedName).HasColumnName("normalized_name").HasMaxLength(120).IsRequired();
            e.Property(x => x.ProductTypeId).HasColumnName("product_type_id");
            e.Property(x => x.DefaultFormat).HasColumnName("default_format").HasMaxLength(20).IsRequired();
            e.Property(x => x.LastUsedAt).HasColumnName("last_used_at").IsRequired();
            e.HasIndex(x => new { x.HouseId, x.NormalizedName }).IsUnique();
            e.HasOne(x => x.ProductType)
                .WithMany()
                .HasForeignKey(x => x.ProductTypeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ShoppingListItem>(e =>
        {
            e.ToTable("shopping_list_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.HouseId).HasColumnName("house_id").IsRequired();
            e.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            e.Property(x => x.ProductTypeId).HasColumnName("product_type_id");
            e.Property(x => x.Format).HasColumnName("format").HasMaxLength(20).IsRequired();
            e.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.HasIndex(x => new { x.HouseId, x.SortOrder });
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ProductType).WithMany().HasForeignKey(x => x.ProductTypeId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BasketItem>(e =>
        {
            e.ToTable("basket_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.HouseId).HasColumnName("house_id").IsRequired();
            e.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            e.Property(x => x.ProductTypeId).HasColumnName("product_type_id");
            e.Property(x => x.Format).HasColumnName("format").HasMaxLength(20).IsRequired();
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(10, 2).IsRequired();
            e.Property(x => x.AddedAt).HasColumnName("added_at").IsRequired();
            e.HasIndex(x => x.HouseId);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ProductType).WithMany().HasForeignKey(x => x.ProductTypeId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductPriceHistory>(e =>
        {
            e.ToTable("product_price_history", t =>
                t.HasCheckConstraint("ck_product_price_dates", "valid_to IS NULL OR valid_to > valid_from"));
            e.HasKey(x => new { x.ProductId, x.StoreId, x.ValidFrom })
                .HasName("pk_product_price_history");
            e.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            e.Property(x => x.StoreId).HasColumnName("store_id").IsRequired();
            e.Property(x => x.ValidFrom).HasColumnName("valid_from").IsRequired();
            e.Property(x => x.ValidTo).HasColumnName("valid_to");
            e.Property(x => x.Price).HasColumnName("price").HasPrecision(10, 2).IsRequired();

            e.HasIndex(x => new { x.ProductId, x.StoreId })
                .HasDatabaseName("ux_product_price_active")
                .IsUnique()
                .HasFilter("valid_to IS NULL");

            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Purchase>(e =>
        {
            e.ToTable("purchases");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.HouseId).HasColumnName("house_id").IsRequired();
            e.Property(x => x.StoreId).HasColumnName("store_id").IsRequired();
            e.Property(x => x.PurchasedAt).HasColumnName("purchased_at").IsRequired();
            e.Property(x => x.CalculatedTotal).HasColumnName("calculated_total").HasPrecision(10, 2).IsRequired();
            e.Property(x => x.ManualTotal).HasColumnName("manual_total").HasPrecision(10, 2);
            e.Property(x => x.ReceiptImageUrl).HasColumnName("receipt_image_url");
            e.HasIndex(x => new { x.HouseId, x.PurchasedAt });
            e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseItem>(e =>
        {
            e.ToTable("purchase_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PurchaseId).HasColumnName("purchase_id").IsRequired();
            e.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            e.Property(x => x.ProductNameSnapshot).HasColumnName("product_name_snapshot").HasMaxLength(120).IsRequired();
            e.Property(x => x.ProductTypeId).HasColumnName("product_type_id");
            e.Property(x => x.Format).HasColumnName("format").HasMaxLength(20).IsRequired();
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(10, 2).IsRequired();
            e.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(10, 2).IsRequired();
            e.Property(x => x.LineTotal).HasColumnName("line_total").HasPrecision(10, 2).IsRequired();
            e.HasOne(x => x.Purchase)
                .WithMany(p => p.Items)
                .HasForeignKey(x => x.PurchaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlatformInvitation>(e =>
        {
            e.ToTable("platform_invitations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Code).HasColumnName("code").HasMaxLength(40).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
            e.Property(x => x.RedeemedAt).HasColumnName("redeemed_at");
            e.Property(x => x.RedeemedByUserId).HasColumnName("redeemed_by_user_id");
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.RedeemedByUser)
                .WithMany()
                .HasForeignKey(x => x.RedeemedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
