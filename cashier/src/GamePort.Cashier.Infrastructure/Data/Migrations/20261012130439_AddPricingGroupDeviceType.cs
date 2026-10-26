using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GamePort.Cashier.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingGroupDeviceType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeviceType",
                table: "PricingGroups",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "PricingGroups");
        }
    }
}
