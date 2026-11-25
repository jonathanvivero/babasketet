using System;
using Babasketet.API.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Babasketet.API.Migrations;

[DbContext(typeof(BabasketetDbContext))]
[Migration("20261125000001_InitialBaseline")]
public sealed class InitialBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "stores",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                house_id = table.Column<Guid>(type: "uuid", nullable: true),
                name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                icon_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                is_default = table.Column<bool>(type: "boolean", nullable: false),
                is_system = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_stores", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "product_types",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                house_id = table.Column<Guid>(type: "uuid", nullable: true),
                name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                fill_hex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                border_hex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                is_system = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_product_types", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "houses",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                active_store_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_houses", x => x.id);
                table.ForeignKey(
                    name: "FK_houses_stores_active_store_id",
                    column: x => x.active_store_id,
                    principalTable: "stores",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "house_members",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                house_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                device_id = table.Column<Guid>(type: "uuid", nullable: true),
                role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_house_members", x => x.id);
                table.CheckConstraint("ck_house_members_identity", "user_id IS NOT NULL OR device_id IS NOT NULL");
                table.ForeignKey(
                    name: "FK_house_members_houses_house_id",
                    column: x => x.house_id,
                    principalTable: "houses",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "house_invitations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                house_id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_house_invitations", x => x.id);
                table.ForeignKey(
                    name: "FK_house_invitations_houses_house_id",
                    column: x => x.house_id,
                    principalTable: "houses",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "products",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                house_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                product_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                default_format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_products", x => x.id);
                table.ForeignKey(
                    name: "FK_products_product_types_product_type_id",
                    column: x => x.product_type_id,
                    principalTable: "product_types",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "purchases",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                house_id = table.Column<Guid>(type: "uuid", nullable: false),
                store_id = table.Column<Guid>(type: "uuid", nullable: false),
                purchased_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                calculated_total = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                manual_total = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                receipt_image_url = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_purchases", x => x.id);
                table.ForeignKey(
                    name: "FK_purchases_stores_store_id",
                    column: x => x.store_id,
                    principalTable: "stores",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "basket_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                house_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_basket_items", x => x.id);
                table.ForeignKey(
                    name: "FK_basket_items_product_types_product_type_id",
                    column: x => x.product_type_id,
                    principalTable: "product_types",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_basket_items_products_product_id",
                    column: x => x.product_id,
                    principalTable: "products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "product_price_history",
            columns: table => new
            {
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                store_id = table.Column<Guid>(type: "uuid", nullable: false),
                valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_product_price_history", x => new { x.product_id, x.store_id, x.valid_from });
                table.CheckConstraint("ck_product_price_dates", "valid_to IS NULL OR valid_to > valid_from");
                table.ForeignKey(
                    name: "FK_product_price_history_products_product_id",
                    column: x => x.product_id,
                    principalTable: "products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_product_price_history_stores_store_id",
                    column: x => x.store_id,
                    principalTable: "stores",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "shopping_list_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                house_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                sort_order = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_shopping_list_items", x => x.id);
                table.ForeignKey(
                    name: "FK_shopping_list_items_product_types_product_type_id",
                    column: x => x.product_type_id,
                    principalTable: "product_types",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_shopping_list_items_products_product_id",
                    column: x => x.product_id,
                    principalTable: "products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "purchase_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                purchase_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_name_snapshot = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                product_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                unit_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                line_total = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_purchase_items", x => x.id);
                table.ForeignKey(
                    name: "FK_purchase_items_purchases_purchase_id",
                    column: x => x.purchase_id,
                    principalTable: "purchases",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_stores_house_id_name", table: "stores", columns: ["house_id", "name"]);
        migrationBuilder.CreateIndex(name: "IX_product_types_house_id_name", table: "product_types", columns: ["house_id", "name"]);
        migrationBuilder.CreateIndex(name: "IX_houses_active_store_id", table: "houses", column: "active_store_id");
        migrationBuilder.CreateIndex(name: "IX_house_members_house_id_user_id", table: "house_members", columns: ["house_id", "user_id"], unique: true, filter: "user_id IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_house_members_house_id_device_id", table: "house_members", columns: ["house_id", "device_id"], unique: true, filter: "device_id IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_house_invitations_house_id", table: "house_invitations", column: "house_id");
        migrationBuilder.CreateIndex(name: "IX_house_invitations_token_hash", table: "house_invitations", column: "token_hash", unique: true);
        migrationBuilder.CreateIndex(name: "IX_products_house_id_normalized_name", table: "products", columns: ["house_id", "normalized_name"], unique: true);
        migrationBuilder.CreateIndex(name: "IX_products_product_type_id", table: "products", column: "product_type_id");
        migrationBuilder.CreateIndex(name: "IX_basket_items_house_id", table: "basket_items", column: "house_id");
        migrationBuilder.CreateIndex(name: "IX_basket_items_product_id", table: "basket_items", column: "product_id");
        migrationBuilder.CreateIndex(name: "IX_basket_items_product_type_id", table: "basket_items", column: "product_type_id");
        migrationBuilder.CreateIndex(name: "ux_product_price_active", table: "product_price_history", columns: ["product_id", "store_id"], unique: true, filter: "valid_to IS NULL");
        migrationBuilder.CreateIndex(name: "IX_product_price_history_store_id", table: "product_price_history", column: "store_id");
        migrationBuilder.CreateIndex(name: "IX_shopping_list_items_house_id_sort_order", table: "shopping_list_items", columns: ["house_id", "sort_order"]);
        migrationBuilder.CreateIndex(name: "IX_shopping_list_items_product_id", table: "shopping_list_items", column: "product_id");
        migrationBuilder.CreateIndex(name: "IX_shopping_list_items_product_type_id", table: "shopping_list_items", column: "product_type_id");
        migrationBuilder.CreateIndex(name: "IX_purchases_house_id_purchased_at", table: "purchases", columns: ["house_id", "purchased_at"]);
        migrationBuilder.CreateIndex(name: "IX_purchases_store_id", table: "purchases", column: "store_id");
        migrationBuilder.CreateIndex(name: "IX_purchase_items_purchase_id", table: "purchase_items", column: "purchase_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "basket_items");
        migrationBuilder.DropTable(name: "house_invitations");
        migrationBuilder.DropTable(name: "house_members");
        migrationBuilder.DropTable(name: "product_price_history");
        migrationBuilder.DropTable(name: "purchase_items");
        migrationBuilder.DropTable(name: "shopping_list_items");
        migrationBuilder.DropTable(name: "houses");
        migrationBuilder.DropTable(name: "products");
        migrationBuilder.DropTable(name: "purchases");
        migrationBuilder.DropTable(name: "product_types");
        migrationBuilder.DropTable(name: "stores");
    }
}
