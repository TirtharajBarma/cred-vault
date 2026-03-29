using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillingService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddAmountPaidToBill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "Bills",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAtUtc",
                table: "Bills",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "PaidAtUtc",
                table: "Bills");
        }
    }
}
