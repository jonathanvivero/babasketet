using System;
using Babasketet.API.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Babasketet.API.Migrations;

[DbContext(typeof(BabasketetDbContext))]
[Migration("20261126000003_AddPlatformInvitations")]
public sealed class AddPlatformInvitations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "platform_invitations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                redeemed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                redeemed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_platform_invitations", x => x.id);
                table.ForeignKey(
                    name: "FK_platform_invitations_app_users_redeemed_by_user_id",
                    column: x => x.redeemed_by_user_id,
                    principalTable: "app_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_platform_invitations_code",
            table: "platform_invitations",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_platform_invitations_created_at",
            table: "platform_invitations",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "IX_platform_invitations_redeemed_by_user_id",
            table: "platform_invitations",
            column: "redeemed_by_user_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "platform_invitations");
    }
}
