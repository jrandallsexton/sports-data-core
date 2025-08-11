using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class LatestV7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DropLowWeeksCount",
                table: "PickemGroup",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 10, 14, 4, 15, 387, DateTimeKind.Utc).AddTicks(1440), new DateTime(2025, 8, 10, 14, 4, 15, 387, DateTimeKind.Utc).AddTicks(1597) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DropLowWeeksCount",
                table: "PickemGroup");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 8, 21, 57, 45, 781, DateTimeKind.Utc).AddTicks(1289), new DateTime(2025, 8, 8, 21, 57, 45, 781, DateTimeKind.Utc).AddTicks(1410) });
        }
    }
}
