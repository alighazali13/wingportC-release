using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GamePort.Cashier.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PricingGroupId",
                table: "PricingRules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "Devices",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(45)",
                oldMaxLength: 45);

            migrationBuilder.AddColumn<int>(
                name: "ConnectionMode",
                table: "Devices",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ConnectivityStatus",
                table: "Devices",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<string>(
                name: "ConsoleModel",
                table: "Devices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ControllerCount",
                table: "Devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Devices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Devices",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMaintenanceAt",
                table: "Devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaintenanceNotes",
                table: "Devices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxPlayers",
                table: "Devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OperationalStatus",
                table: "Devices",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "PricingGroupId",
                table: "Devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ZoneId",
                table: "Devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PricingGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Zones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Zones_Zones_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_PricingGroupId",
                table: "PricingRules",
                column: "PricingGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_IsActive",
                table: "Devices",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_PricingGroupId",
                table: "Devices",
                column: "PricingGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ZoneId",
                table: "Devices",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingGroups_IsActive",
                table: "PricingGroups",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Zones_DisplayOrder",
                table: "Zones",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Zones_IsActive",
                table: "Zones",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Zones_ParentId",
                table: "Zones",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_PricingGroups_PricingGroupId",
                table: "Devices",
                column: "PricingGroupId",
                principalTable: "PricingGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Zones_ZoneId",
                table: "Devices",
                column: "ZoneId",
                principalTable: "Zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PricingRules_PricingGroups_PricingGroupId",
                table: "PricingRules",
                column: "PricingGroupId",
                principalTable: "PricingGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ===== BACKFILL: Map old Status to new V2 fields =====
            // Old DeviceStatus: Available=1, InUse=2, Offline=3, Disconnected=4, Maintenance=5
            // New OperationalStatus: Available=1, InUse=2, Maintenance=3
            // New ConnectivityStatus: Connected=1, Disconnected=2, NotApplicable=3
            // New ConnectionMode: Client=1, Manual=2

            // OperationalStatus: explicit CASE mapping from old Status
            migrationBuilder.Sql(@"
                UPDATE ""Devices"" SET ""OperationalStatus"" = CASE ""Status""
                    WHEN 1 THEN 1  -- Available -> Available
                    WHEN 2 THEN 2  -- InUse -> InUse
                    WHEN 3 THEN 1  -- Offline -> Available (operational)
                    WHEN 4 THEN 1  -- Disconnected -> Available (operational)
                    WHEN 5 THEN 3  -- Maintenance -> Maintenance
                    ELSE 1
                END;
            ");

            // ConnectivityStatus: PS5 -> NotApplicable; PC + IsConnected -> Connected/Disconnected
            migrationBuilder.Sql(@"
                UPDATE ""Devices"" SET ""ConnectivityStatus"" = CASE
                    WHEN ""Type"" = 2 THEN 3                    -- PlayStation -> NotApplicable
                    WHEN ""IsConnected"" = true THEN 1         -- GamingPC + connected -> Connected
                    ELSE 2                                     -- GamingPC + not connected -> Disconnected
                END;
            ");

            // ConnectionMode: PS5 -> Manual; PC -> Client
            migrationBuilder.Sql(@"
                UPDATE ""Devices"" SET ""ConnectionMode"" = CASE
                    WHEN ""Type"" = 2 THEN 2  -- PlayStation -> Manual
                    ELSE 1                    -- GamingPC -> Client
                END;
            ");

            // IsActive: soft-deleted devices are inactive
            migrationBuilder.Sql(@"
                UPDATE ""Devices"" SET ""IsActive"" = NOT ""IsDeleted"";
            ");

            // PS5 defaults (application-layer convention)
            migrationBuilder.Sql(@"
                UPDATE ""Devices"" SET ""ControllerCount"" = 2
                    WHERE ""Type"" = 2 AND ""ControllerCount"" IS NULL;
            ");
            migrationBuilder.Sql(@"
                UPDATE ""Devices"" SET ""MaxPlayers"" = 1
                    WHERE ""Type"" = 2 AND ""MaxPlayers"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_PricingGroups_PricingGroupId",
                table: "Devices");

            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Zones_ZoneId",
                table: "Devices");

            migrationBuilder.DropForeignKey(
                name: "FK_PricingRules_PricingGroups_PricingGroupId",
                table: "PricingRules");

            migrationBuilder.DropTable(
                name: "PricingGroups");

            migrationBuilder.DropTable(
                name: "Zones");

            migrationBuilder.DropIndex(
                name: "IX_PricingRules_PricingGroupId",
                table: "PricingRules");

            migrationBuilder.DropIndex(
                name: "IX_Devices_IsActive",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_PricingGroupId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_ZoneId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "PricingGroupId",
                table: "PricingRules");

            migrationBuilder.DropColumn(
                name: "ConnectionMode",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ConnectivityStatus",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ConsoleModel",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ControllerCount",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LastMaintenanceAt",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MaintenanceNotes",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MaxPlayers",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "OperationalStatus",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "PricingGroupId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ZoneId",
                table: "Devices");

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "Devices",
                type: "character varying(45)",
                maxLength: 45,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(45)",
                oldMaxLength: 45,
                oldNullable: true);
        }
    }
}
