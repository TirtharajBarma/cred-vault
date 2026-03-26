using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillingService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddIssuerRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IssuerId",
                table: "RewardTiers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IssuerId",
                table: "Bills",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IssuerId",
                table: "RewardTiers");

            migrationBuilder.DropColumn(
                name: "IssuerId",
                table: "Bills");
        }
    }
}
