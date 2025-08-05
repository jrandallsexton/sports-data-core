using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class Latest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "PickemGroupConference",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PickemGroup",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RankingFilter",
                table: "PickemGroup",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 5, 14, 18, 52, 337, DateTimeKind.Utc).AddTicks(2540), new DateTime(2025, 8, 5, 14, 18, 52, 337, DateTimeKind.Utc).AddTicks(2657) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Slug",
                table: "PickemGroupConference");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "PickemGroup");

            migrationBuilder.DropColumn(
                name: "RankingFilter",
                table: "PickemGroup");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 5, 11, 46, 43, 592, DateTimeKind.Utc).AddTicks(4399), new DateTime(2025, 8, 5, 11, 46, 43, 592, DateTimeKind.Utc).AddTicks(4515) });
        }
    }
}
