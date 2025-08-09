using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class LatestV6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPanelPersona",
                table: "User",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSynthetic",
                table: "User",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "IsPanelPersona", "IsSynthetic", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 8, 21, 57, 45, 781, DateTimeKind.Utc).AddTicks(1289), false, false, new DateTime(2025, 8, 8, 21, 57, 45, 781, DateTimeKind.Utc).AddTicks(1410) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPanelPersona",
                table: "User");

            migrationBuilder.DropColumn(
                name: "IsSynthetic",
                table: "User");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 6, 12, 41, 31, 403, DateTimeKind.Utc).AddTicks(1620), new DateTime(2025, 8, 6, 12, 41, 31, 403, DateTimeKind.Utc).AddTicks(1740) });
        }
    }
}
