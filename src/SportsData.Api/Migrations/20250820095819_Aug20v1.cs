using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class Aug20v1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PickemGroupMatchup",
                table: "PickemGroupMatchup");

            migrationBuilder.DropIndex(
                name: "IX_PickemGroupMatchup_GroupId_SeasonWeekId",
                table: "PickemGroupMatchup");

            migrationBuilder.AddColumn<int>(
                name: "AwayRank",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeRank",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PickemGroupMatchup",
                table: "PickemGroupMatchup",
                columns: new[] { "GroupId", "SeasonWeekId", "ContestId" });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 20, 9, 58, 18, 909, DateTimeKind.Utc).AddTicks(294), new DateTime(2025, 8, 20, 9, 58, 18, 909, DateTimeKind.Utc).AddTicks(408) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PickemGroupMatchup",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "AwayRank",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "HomeRank",
                table: "PickemGroupMatchup");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PickemGroupMatchup",
                table: "PickemGroupMatchup",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 19, 23, 0, 38, 386, DateTimeKind.Utc).AddTicks(3941), new DateTime(2025, 8, 19, 23, 0, 38, 386, DateTimeKind.Utc).AddTicks(4059) });

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupMatchup_GroupId_SeasonWeekId",
                table: "PickemGroupMatchup",
                columns: new[] { "GroupId", "SeasonWeekId" });
        }
    }
}
