using System;
using Babasketet.API.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Babasketet.API.Migrations;

[DbContext(typeof(BabasketetDbContext))]
[Migration("20261126000002_AddAdminNotifications")]
public sealed class AddAdminNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "admin_notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                body = table.Column<string>(type: "text", nullable: false),
                category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_notifications", x => x.id);
                table.CheckConstraint("ck_admin_notifications_status", "status IN ('New', 'Read', 'Archived')");
            });

        migrationBuilder.CreateIndex(
            name: "IX_admin_notifications_status_created_at",
            table: "admin_notifications",
            columns: ["status", "created_at"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "admin_notifications");
    }
}
