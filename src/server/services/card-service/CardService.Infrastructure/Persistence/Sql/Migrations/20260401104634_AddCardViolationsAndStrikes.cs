using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddCardViolationsAndStrikes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BlockedAtUtc",
                table: "CreditCards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBlocked",
                table: "CreditCards",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StrikeCount",
                table: "CreditCards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnblockedAtUtc",
                table: "CreditCards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CardViolations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    StrikeCount = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PenaltyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClearedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardViolations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardViolations_CreditCards_CardId",
                        column: x => x.CardId,
                        principalTable: "CreditCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardViolations_CardId",
                table: "CardViolations",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_CardViolations_CardId_IsActive",
                table: "CardViolations",
                columns: new[] { "CardId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CardViolations_UserId",
                table: "CardViolations",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardViolations");

            migrationBuilder.DropColumn(
                name: "BlockedAtUtc",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "IsBlocked",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "StrikeCount",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "UnblockedAtUtc",
                table: "CreditCards");
        }
    }
}
