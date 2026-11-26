using System;
using Babasketet.API.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Babasketet.API.Migrations;

[DbContext(typeof(BabasketetDbContext))]
[Migration("20261126000001_AddSystemEmailConfig")]
public sealed class AddSystemEmailConfig : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "system_email_configs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                port = table.Column<int>(type: "integer", nullable: false),
                username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                encrypted_password = table.Column<string>(type: "text", nullable: true),
                security_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                from_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                from_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                enabled = table.Column<bool>(type: "boolean", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_system_email_configs", x => x.id);
                table.CheckConstraint("ck_system_email_configs_port", "port BETWEEN 1 AND 65535");
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "system_email_configs");
    }
}
