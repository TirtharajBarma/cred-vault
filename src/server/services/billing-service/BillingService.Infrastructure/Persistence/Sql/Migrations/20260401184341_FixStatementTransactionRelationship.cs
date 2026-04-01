using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BillingService.Infrastructure.Persistence.Sql.Migrations
{
    /// <inheritdoc />
    public partial class FixStatementTransactionRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StatementTransactions_Statements_StatementId1",
                table: "StatementTransactions");

            migrationBuilder.DropIndex(
                name: "IX_StatementTransactions_StatementId1",
                table: "StatementTransactions");

            migrationBuilder.DropColumn(
                name: "StatementId1",
                table: "StatementTransactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StatementId1",
                table: "StatementTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatementTransactions_StatementId1",
                table: "StatementTransactions",
                column: "StatementId1");

            migrationBuilder.AddForeignKey(
                name: "FK_StatementTransactions_Statements_StatementId1",
                table: "StatementTransactions",
                column: "StatementId1",
                principalTable: "Statements",
                principalColumn: "Id");
        }
    }
}
