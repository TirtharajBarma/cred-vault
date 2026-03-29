using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerificationOtp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailVerificationOtp",
                table: "identity_users",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerificationOtpExpiresAtUtc",
                table: "identity_users",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailVerificationOtp",
                table: "identity_users");

            migrationBuilder.DropColumn(
                name: "EmailVerificationOtpExpiresAtUtc",
                table: "identity_users");
        }
    }
}
