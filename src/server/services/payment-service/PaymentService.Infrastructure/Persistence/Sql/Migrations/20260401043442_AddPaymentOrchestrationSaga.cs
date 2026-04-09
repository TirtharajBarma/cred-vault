using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentOrchestrationSaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentOrchestrationSagas",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    RiskScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    RiskDecision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PaymentProcessed = table.Column<bool>(type: "bit", nullable: false),
                    BillUpdated = table.Column<bool>(type: "bit", nullable: false),
                    CardDeducted = table.Column<bool>(type: "bit", nullable: false),
                    PaymentError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BillUpdateError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CardDeductionError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompensationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompensationAttempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentOrchestrationSagas", x => x.CorrelationId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentOrchestrationSagas");
        }
    }
}
