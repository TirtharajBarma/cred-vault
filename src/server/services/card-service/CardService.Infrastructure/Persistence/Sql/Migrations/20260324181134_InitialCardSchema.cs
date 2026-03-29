using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class InitialCardSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardIssuers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Network = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardIssuers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreditCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardholderName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExpMonth = table.Column<int>(type: "int", nullable: false),
                    ExpYear = table.Column<int>(type: "int", nullable: false),
                    Last4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    MaskedNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutstandingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BillingCycleStartDay = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditCards_CardIssuers_IssuerId",
                        column: x => x.IssuerId,
                        principalTable: "CardIssuers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardIssuers_Network",
                table: "CardIssuers",
                column: "Network",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditCards_IssuerId",
                table: "CreditCards",
                column: "IssuerId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditCards_UserId",
                table: "CreditCards",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditCards_UserId_IsDefault",
                table: "CreditCards",
                columns: new[] { "UserId", "IsDefault" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditCards");

            migrationBuilder.DropTable(
                name: "CardIssuers");
        }
    }
}
