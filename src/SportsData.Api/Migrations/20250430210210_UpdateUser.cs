using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Timezone",
                table: "User",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc", "Timezone" },
                values: new object[] { new DateTime(2025, 4, 30, 21, 2, 10, 8, DateTimeKind.Utc).AddTicks(1780), new DateTime(2025, 4, 30, 21, 2, 10, 8, DateTimeKind.Utc).AddTicks(1902), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Timezone",
                table: "User");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 4, 30, 10, 55, 44, 710, DateTimeKind.Utc).AddTicks(1093), new DateTime(2025, 4, 30, 10, 55, 44, 710, DateTimeKind.Utc).AddTicks(1182) });
        }
    }
}
