using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillingService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class InitialBillingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardNetwork = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    BillingDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RewardTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardNetwork = table.Column<int>(type: "int", nullable: false),
                    MinSpend = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RewardRate = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardTiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentSchedules_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RewardAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RewardTierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PointsBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewardAccounts_RewardTiers_RewardTierId",
                        column: x => x.RewardTierId,
                        principalTable: "RewardTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RewardTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RewardAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Points = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewardTransactions_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RewardTransactions_RewardAccounts_RewardAccountId",
                        column: x => x.RewardAccountId,
                        principalTable: "RewardAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_DueDateUtc",
                table: "Bills",
                column: "DueDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_UserId",
                table: "Bills",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_UserId_Status",
                table: "Bills",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSchedules_BillId",
                table: "PaymentSchedules",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSchedules_PaymentDateUtc",
                table: "PaymentSchedules",
                column: "PaymentDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RewardAccounts_RewardTierId",
                table: "RewardAccounts",
                column: "RewardTierId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardAccounts_UserId",
                table: "RewardAccounts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewardTiers_CardNetwork_EffectiveFromUtc",
                table: "RewardTiers",
                columns: new[] { "CardNetwork", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardTransactions_BillId",
                table: "RewardTransactions",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardTransactions_CreatedAtUtc",
                table: "RewardTransactions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RewardTransactions_RewardAccountId",
                table: "RewardTransactions",
                column: "RewardAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentSchedules");

            migrationBuilder.DropTable(
                name: "RewardTransactions");

            migrationBuilder.DropTable(
                name: "Bills");

            migrationBuilder.DropTable(
                name: "RewardAccounts");

            migrationBuilder.DropTable(
                name: "RewardTiers");
        }
    }
}
