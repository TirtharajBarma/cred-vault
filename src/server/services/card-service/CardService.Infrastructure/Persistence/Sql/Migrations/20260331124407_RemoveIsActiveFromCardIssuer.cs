using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsActiveFromCardIssuer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CardIssuers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CardIssuers",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }
    }
}
