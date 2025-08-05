using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class LatestV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 5, 19, 25, 19, 474, DateTimeKind.Utc).AddTicks(1946), new DateTime(2025, 8, 5, 19, 25, 19, 474, DateTimeKind.Utc).AddTicks(2067) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 5, 19, 19, 18, 958, DateTimeKind.Utc).AddTicks(6923), new DateTime(2025, 8, 5, 19, 19, 18, 958, DateTimeKind.Utc).AddTicks(7038) });
        }
    }
}
