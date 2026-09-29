using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GamePort.Cashier.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservations_DeviceId",
                table: "Reservations");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Reservations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "Reservations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConvertedAt",
                table: "Reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConvertedSessionId",
                table: "Reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeviceType",
                table: "Reservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_DeviceId_StartTime",
                table: "Reservations",
                columns: new[] { "DeviceId", "StartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservations_DeviceId_StartTime",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ConvertedAt",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ConvertedSessionId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "Reservations");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Reservations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "Reservations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_DeviceId",
                table: "Reservations",
                column: "DeviceId");
        }
    }
}
