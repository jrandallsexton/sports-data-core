using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class Aug19V1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "OverOdds",
                table: "PickemGroupMatchup",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Spread",
                table: "PickemGroupMatchup",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "UnderOdds",
                table: "PickemGroupMatchup",
                type: "double precision",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 19, 23, 0, 38, 386, DateTimeKind.Utc).AddTicks(3941), new DateTime(2025, 8, 19, 23, 0, 38, 386, DateTimeKind.Utc).AddTicks(4059) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverOdds",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "Spread",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "UnderOdds",
                table: "PickemGroupMatchup");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 18, 0, 29, 55, 526, DateTimeKind.Utc).AddTicks(7865), new DateTime(2025, 8, 18, 0, 29, 55, 526, DateTimeKind.Utc).AddTicks(8009) });
        }
    }
}
