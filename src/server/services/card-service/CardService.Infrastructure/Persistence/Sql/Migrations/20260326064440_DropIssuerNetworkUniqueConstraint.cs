using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class DropIssuerNetworkUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CardIssuers_Network",
                table: "CardIssuers");

            migrationBuilder.CreateIndex(
                name: "IX_CardIssuers_Network",
                table: "CardIssuers",
                column: "Network");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CardIssuers_Network",
                table: "CardIssuers");

            migrationBuilder.CreateIndex(
                name: "IX_CardIssuers_Network",
                table: "CardIssuers",
                column: "Network",
                unique: true);
        }
    }
}
