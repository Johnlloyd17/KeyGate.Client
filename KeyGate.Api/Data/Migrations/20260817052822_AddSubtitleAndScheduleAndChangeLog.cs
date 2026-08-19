using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KeyGate.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubtitleAndScheduleAndChangeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "ScheduleAutoLock",
                table: "LockScreenConfigs",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ScheduleAutoUnlock",
                table: "LockScreenConfigs",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subtitle",
                table: "LockScreenConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConfigChangeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<int>(type: "integer", nullable: true),
                    ChangedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FieldChanged = table.Column<string>(type: "text", nullable: false),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfigChangeLogs_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigChangeLogs_DeviceId",
                table: "ConfigChangeLogs",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigChangeLogs");

            migrationBuilder.DropColumn(
                name: "ScheduleAutoLock",
                table: "LockScreenConfigs");

            migrationBuilder.DropColumn(
                name: "ScheduleAutoUnlock",
                table: "LockScreenConfigs");

            migrationBuilder.DropColumn(
                name: "Subtitle",
                table: "LockScreenConfigs");
        }
    }
}
