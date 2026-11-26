using System;
using Babasketet.API.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Babasketet.API.Migrations;

[DbContext(typeof(BabasketetDbContext))]
[Migration("20261125000002_AddAppUsersAndOwnershipRules")]
public sealed class AddAppUsersAndOwnershipRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "app_users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                zitadel_user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                device_id = table.Column<Guid>(type: "uuid", nullable: true),
                global_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                is_admin = table.Column<bool>(type: "boolean", nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                auth_method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                custom_properties_json = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_app_users", x => x.id);
                table.CheckConstraint("ck_app_users_identity", "zitadel_user_id IS NOT NULL OR device_id IS NOT NULL");
                table.CheckConstraint(
                    "ck_app_users_global_role",
                    "(global_role = 'Guest' AND zitadel_user_id IS NULL AND is_admin = FALSE) OR (global_role = 'User' AND zitadel_user_id IS NOT NULL)");
                table.CheckConstraint("ck_app_users_status", "status IN ('Active', 'Blocked')");
            });

        migrationBuilder.CreateIndex(
            name: "IX_app_users_zitadel_user_id",
            table: "app_users",
            column: "zitadel_user_id",
            unique: true,
            filter: "zitadel_user_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_app_users_device_id",
            table: "app_users",
            column: "device_id",
            unique: true,
            filter: "device_id IS NOT NULL");

        // Data migration: solo usuarios registrados (User) pueden ser Owner (maximo 1 por casa).
        // 1. Degradar miembros anonimos que previamente figuraban como 'owner' a 'member'.
        migrationBuilder.Sql("""
            UPDATE house_members
            SET role = 'member'
            WHERE role = 'owner' AND user_id IS NULL;
            """);

        // 2. Si una casa tiene varios owners registrados historicos, conservar solo el mas antiguo.
        migrationBuilder.Sql("""
            WITH ranked_owners AS (
                SELECT id,
                       ROW_NUMBER() OVER (PARTITION BY house_id ORDER BY joined_at ASC, id ASC) AS rn
                FROM house_members
                WHERE role = 'owner' AND user_id IS NOT NULL
            )
            UPDATE house_members hm
            SET role = 'member'
            FROM ranked_owners ro
            WHERE hm.id = ro.id AND ro.rn > 1;
            """);

        // 3. Si una casa tiene miembros registrados sin ningun owner, promover al mas antiguo.
        migrationBuilder.Sql("""
            WITH houses_without_owner AS (
                SELECT DISTINCT house_id
                FROM house_members
                WHERE user_id IS NOT NULL
                EXCEPT
                SELECT house_id
                FROM house_members
                WHERE role = 'owner' AND user_id IS NOT NULL
            ),
            first_registered AS (
                SELECT hm.id,
                       ROW_NUMBER() OVER (PARTITION BY hm.house_id ORDER BY hm.joined_at ASC, hm.id ASC) AS rn
                FROM house_members hm
                INNER JOIN houses_without_owner hwo ON hwo.house_id = hm.house_id
                WHERE hm.user_id IS NOT NULL
            )
            UPDATE house_members hm
            SET role = 'owner'
            FROM first_registered fr
            WHERE hm.id = fr.id AND fr.rn = 1;
            """);

        // 4. Poblar app_users para usuarios registrados y dispositivos Guest existentes en house_members.
        migrationBuilder.Sql("""
            INSERT INTO app_users (id, zitadel_user_id, device_id, global_role, is_admin, status, email, display_name, auth_method, custom_properties_json, created_at, last_seen_at)
            SELECT DISTINCT ON (user_id)
                id,
                user_id,
                NULL,
                'User',
                FALSE,
                'Active',
                NULL,
                user_id,
                'zitadel',
                '{}',
                joined_at,
                joined_at
            FROM house_members
            WHERE user_id IS NOT NULL
            ORDER BY user_id, joined_at ASC
            ON CONFLICT DO NOTHING;

            INSERT INTO app_users (id, zitadel_user_id, device_id, global_role, is_admin, status, email, display_name, auth_method, custom_properties_json, created_at, last_seen_at)
            SELECT DISTINCT ON (device_id)
                id,
                NULL,
                device_id,
                'Guest',
                FALSE,
                'Active',
                NULL,
                'Invitado ' || LEFT(device_id::text, 8),
                'anonymous',
                '{}',
                joined_at,
                joined_at
            FROM house_members
            WHERE user_id IS NULL AND device_id IS NOT NULL
            ORDER BY device_id, joined_at ASC
            ON CONFLICT DO NOTHING;
            """);

        migrationBuilder.AddCheckConstraint(
            name: "ck_house_members_owner_registered",
            table: "house_members",
            sql: "role <> 'owner' OR user_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ux_house_members_single_owner",
            table: "house_members",
            column: "house_id",
            unique: true,
            filter: "role = 'owner' AND user_id IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_house_members_single_owner",
            table: "house_members");

        migrationBuilder.DropCheckConstraint(
            name: "ck_house_members_owner_registered",
            table: "house_members");

        migrationBuilder.DropTable(
            name: "app_users");
    }
}
