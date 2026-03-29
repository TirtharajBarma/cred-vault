using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillingService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddReversedAtToRewardTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReversedAtUtc",
                table: "RewardTransactions",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReversedAtUtc",
                table: "RewardTransactions");
        }
    }
}
