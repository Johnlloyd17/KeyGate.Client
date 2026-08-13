using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyGate.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceApiKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceApiKeyHash",
                table: "Devices",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceApiKeyHash",
                table: "Devices");
        }
    }
}
