using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WattWise.Migrations
{
    /// <inheritdoc />
    public partial class AddSolarSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrGridExportReading",
                table: "MeterReadings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrSolarGenReading",
                table: "MeterReadings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrevGridExportReading",
                table: "MeterReadings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrevSolarGenReading",
                table: "MeterReadings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasBattery",
                table: "Connections",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasSolar",
                table: "Connections",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SolarCapacityKw",
                table: "Connections",
                type: "decimal(8,3)",
                precision: 8,
                scale: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrGridExportReading",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "CurrSolarGenReading",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "PrevGridExportReading",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "PrevSolarGenReading",
                table: "MeterReadings");

            migrationBuilder.DropColumn(
                name: "HasBattery",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "HasSolar",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "SolarCapacityKw",
                table: "Connections");
        }
    }
}
