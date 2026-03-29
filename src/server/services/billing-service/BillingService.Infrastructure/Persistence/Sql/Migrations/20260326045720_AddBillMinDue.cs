using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillingService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddBillMinDue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MinDue",
                table: "Bills",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinDue",
                table: "Bills");
        }
    }
}
