using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePaymentOrchestrationSaga_OtpFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OtpCode",
                table: "PaymentOrchestrationSagas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OtpExpiresAtUtc",
                table: "PaymentOrchestrationSagas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OtpVerified",
                table: "PaymentOrchestrationSagas",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OtpCode",
                table: "PaymentOrchestrationSagas");

            migrationBuilder.DropColumn(
                name: "OtpExpiresAtUtc",
                table: "PaymentOrchestrationSagas");

            migrationBuilder.DropColumn(
                name: "OtpVerified",
                table: "PaymentOrchestrationSagas");
        }
    }
}
