using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class Aug21v1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PickType",
                table: "UserPick",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDateUtc",
                table: "PickemGroupMatchup",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 21, 11, 38, 36, 870, DateTimeKind.Utc).AddTicks(3497), new DateTime(2025, 8, 21, 11, 38, 36, 870, DateTimeKind.Utc).AddTicks(3661) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PickType",
                table: "UserPick");

            migrationBuilder.DropColumn(
                name: "StartDateUtc",
                table: "PickemGroupMatchup");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 20, 18, 47, 33, 713, DateTimeKind.Utc).AddTicks(7433), new DateTime(2025, 8, 20, 18, 47, 33, 713, DateTimeKind.Utc).AddTicks(7532) });
        }
    }
}
