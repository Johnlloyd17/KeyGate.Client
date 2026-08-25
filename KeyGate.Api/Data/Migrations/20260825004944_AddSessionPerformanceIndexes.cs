using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyGate.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_DeviceId",
                table: "Sessions");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_DeviceId_EndedAt",
                table: "Sessions",
                columns: new[] { "DeviceId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_StartedAt",
                table: "Sessions",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_DeviceId_EndedAt",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_StartedAt",
                table: "Sessions");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_DeviceId",
                table: "Sessions",
                column: "DeviceId");
        }
    }
}
