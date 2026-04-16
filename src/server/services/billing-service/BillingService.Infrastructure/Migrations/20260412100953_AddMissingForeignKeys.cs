using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Statements_BillId",
                table: "Statements",
                column: "BillId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Statements_Bills_BillId",
                table: "Statements",
                column: "BillId",
                principalTable: "Bills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RewardAccounts_RewardTiers_RewardTierId",
                table: "RewardAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Statements_Bills_BillId",
                table: "Statements");

            migrationBuilder.DropIndex(
                name: "IX_Statements_BillId",
                table: "Statements");

            migrationBuilder.DropIndex(
                name: "IX_RewardAccounts_RewardTierId",
                table: "RewardAccounts");
        }
    }
}
