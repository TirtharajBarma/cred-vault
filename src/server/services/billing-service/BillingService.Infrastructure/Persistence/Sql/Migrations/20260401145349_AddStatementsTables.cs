using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillingService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddStatementsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Statements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StatementPeriod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPurchases = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPayments = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalRefunds = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PenaltyCharges = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InterestCharges = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinimumDue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CardLast4 = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CardNetwork = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IssuerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AvailableCredit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Statements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatementTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatementTransactions_Statements_StatementId",
                        column: x => x.StatementId,
                        principalTable: "Statements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StatementTransactions_Statements_StatementId1",
                        column: x => x.StatementId1,
                        principalTable: "Statements",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Statements_CardId",
                table: "Statements",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_Statements_PeriodEndUtc",
                table: "Statements",
                column: "PeriodEndUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Statements_UserId",
                table: "Statements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Statements_UserId_Status",
                table: "Statements",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StatementTransactions_DateUtc",
                table: "StatementTransactions",
                column: "DateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StatementTransactions_StatementId",
                table: "StatementTransactions",
                column: "StatementId");

            migrationBuilder.CreateIndex(
                name: "IX_StatementTransactions_StatementId1",
                table: "StatementTransactions",
                column: "StatementId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StatementTransactions");

            migrationBuilder.DropTable(
                name: "Statements");
        }
    }
}
