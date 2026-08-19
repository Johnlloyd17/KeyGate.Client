using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyGate.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveScheduleFromLockScreenConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScheduleAutoLock",
                table: "LockScreenConfigs");

            migrationBuilder.DropColumn(
                name: "ScheduleAutoUnlock",
                table: "LockScreenConfigs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
