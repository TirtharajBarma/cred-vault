using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillingService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class MakeRewardTierIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RewardAccounts_RewardTiers_RewardTierId",
                table: "RewardAccounts");

            migrationBuilder.DropIndex(
                name: "IX_RewardAccounts_RewardTierId",
                table: "RewardAccounts");

            migrationBuilder.AlterColumn<Guid>(
                name: "RewardTierId",
                table: "RewardAccounts",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "RewardTierId",
                table: "RewardAccounts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewardAccounts_RewardTierId",
                table: "RewardAccounts",
                column: "RewardTierId");

            migrationBuilder.AddForeignKey(
                name: "FK_RewardAccounts_RewardTiers_RewardTierId",
                table: "RewardAccounts",
                column: "RewardTierId",
                principalTable: "RewardTiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
