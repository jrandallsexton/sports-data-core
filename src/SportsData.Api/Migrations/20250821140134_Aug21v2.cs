using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class Aug21v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPick_Contest_ContestId",
                table: "UserPick");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Contest_ContestId",
                table: "Contest");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 21, 14, 1, 34, 374, DateTimeKind.Utc).AddTicks(9783), new DateTime(2025, 8, 21, 14, 1, 34, 374, DateTimeKind.Utc).AddTicks(9884) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Contest_ContestId",
                table: "Contest",
                column: "ContestId");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 21, 11, 38, 36, 870, DateTimeKind.Utc).AddTicks(3497), new DateTime(2025, 8, 21, 11, 38, 36, 870, DateTimeKind.Utc).AddTicks(3661) });

            migrationBuilder.AddForeignKey(
                name: "FK_UserPick_Contest_ContestId",
                table: "UserPick",
                column: "ContestId",
                principalTable: "Contest",
                principalColumn: "ContestId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
