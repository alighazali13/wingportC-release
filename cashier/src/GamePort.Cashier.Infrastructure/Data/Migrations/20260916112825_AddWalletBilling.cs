using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GamePort.Cashier.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BalanceAfter",
                table: "WalletTransactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "WalletTransactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastChargedAt",
                table: "Sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCharged",
                table: "Sessions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_IdempotencyKey",
                table: "WalletTransactions",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_IdempotencyKey",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "BalanceAfter",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "LastChargedAt",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "TotalCharged",
                table: "Sessions");
        }
    }
}
