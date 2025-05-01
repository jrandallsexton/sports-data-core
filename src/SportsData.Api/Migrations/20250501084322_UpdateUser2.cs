using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUser2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 5, 1, 8, 43, 22, 91, DateTimeKind.Utc).AddTicks(3122), new DateTime(2025, 5, 1, 8, 43, 22, 91, DateTimeKind.Utc).AddTicks(3237) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 4, 30, 21, 2, 10, 8, DateTimeKind.Utc).AddTicks(1780), new DateTime(2025, 4, 30, 21, 2, 10, 8, DateTimeKind.Utc).AddTicks(1902) });
        }
    }
}
