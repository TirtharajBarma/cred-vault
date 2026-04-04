using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardsFieldsToSaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RewardsAmount",
                table: "PaymentOrchestrationSagas",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "RewardsRedeemed",
                table: "PaymentOrchestrationSagas",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RewardsAmount",
                table: "PaymentOrchestrationSagas");

            migrationBuilder.DropColumn(
                name: "RewardsRedeemed",
                table: "PaymentOrchestrationSagas");
        }
    }
}
