using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyGate.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExcelFieldsToIndividual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Age",
                table: "Individuals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Barangay",
                table: "Individuals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CityMunicipality",
                table: "Individuals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Province",
                table: "Individuals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sectors",
                table: "Individuals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceAvailed",
                table: "Individuals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sex",
                table: "Individuals",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Age",
                table: "Individuals");

            migrationBuilder.DropColumn(
                name: "Barangay",
                table: "Individuals");

            migrationBuilder.DropColumn(
                name: "CityMunicipality",
                table: "Individuals");

            migrationBuilder.DropColumn(
                name: "Province",
                table: "Individuals");

            migrationBuilder.DropColumn(
                name: "Sectors",
                table: "Individuals");

            migrationBuilder.DropColumn(
                name: "ServiceAvailed",
                table: "Individuals");

            migrationBuilder.DropColumn(
                name: "Sex",
                table: "Individuals");
        }
    }
}
