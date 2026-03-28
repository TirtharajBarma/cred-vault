using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityService.Infrastructure.Persistence.Sql.Migrations;

public partial class AddPasswordResetFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PasswordResetOtp",
            table: "identity_users",
            type: "nvarchar(16)",
            maxLength: 16,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "PasswordResetOtpExpiresAtUtc",
            table: "identity_users",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PasswordResetOtp",
            table: "identity_users");

        migrationBuilder.DropColumn(
            name: "PasswordResetOtpExpiresAtUtc",
            table: "identity_users");
    }
}
