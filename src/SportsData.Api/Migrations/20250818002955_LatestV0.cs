using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class LatestV0 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "PickemGroupWeek",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "PickemGroupWeek",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "PickemGroupWeek",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedBy",
                table: "PickemGroupWeek",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedUtc",
                table: "PickemGroupWeek",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 18, 0, 29, 55, 526, DateTimeKind.Utc).AddTicks(7865), new DateTime(2025, 8, 18, 0, 29, 55, 526, DateTimeKind.Utc).AddTicks(8009) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PickemGroupWeek");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "PickemGroupWeek");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "PickemGroupWeek");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "PickemGroupWeek");

            migrationBuilder.DropColumn(
                name: "ModifiedUtc",
                table: "PickemGroupWeek");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 18, 0, 17, 31, 718, DateTimeKind.Utc).AddTicks(1441), new DateTime(2025, 8, 18, 0, 17, 31, 718, DateTimeKind.Utc).AddTicks(1559) });
        }
    }
}
