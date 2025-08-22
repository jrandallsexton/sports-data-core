using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class Aug22v1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPick_ContestId",
                table: "UserPick");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "User",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 22, 13, 6, 53, 217, DateTimeKind.Utc).AddTicks(3193), new DateTime(2025, 8, 22, 13, 6, 53, 217, DateTimeKind.Utc).AddTicks(3286) });

            migrationBuilder.CreateIndex(
                name: "IX_UserPick_ContestId",
                table: "UserPick",
                column: "ContestId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPick_UserId",
                table: "UserPick",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserPick_User_UserId",
                table: "UserPick",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPick_User_UserId",
                table: "UserPick");

            migrationBuilder.DropIndex(
                name: "IX_UserPick_ContestId",
                table: "UserPick");

            migrationBuilder.DropIndex(
                name: "IX_UserPick_UserId",
                table: "UserPick");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "User",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 21, 19, 57, 38, 538, DateTimeKind.Utc).AddTicks(8305), new DateTime(2025, 8, 21, 19, 57, 38, 538, DateTimeKind.Utc).AddTicks(8425) });

            migrationBuilder.CreateIndex(
                name: "IX_UserPick_ContestId",
                table: "UserPick",
                column: "ContestId",
                unique: true);
        }
    }
}
