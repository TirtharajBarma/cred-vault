using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentService.Infrastructure.Migrations
{
    public partial class AddRazorpayWalletTopUps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RazorpayWalletTopUps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    RazorpayOrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RazorpayPaymentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RazorpaySignature = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RazorpayWalletTopUps", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RazorpayWalletTopUps_RazorpayOrderId",
                table: "RazorpayWalletTopUps",
                column: "RazorpayOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RazorpayWalletTopUps_RazorpayPaymentId",
                table: "RazorpayWalletTopUps",
                column: "RazorpayPaymentId",
                unique: true,
                filter: "[RazorpayPaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RazorpayWalletTopUps_UserId",
                table: "RazorpayWalletTopUps",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RazorpayWalletTopUps");
        }
    }
}
