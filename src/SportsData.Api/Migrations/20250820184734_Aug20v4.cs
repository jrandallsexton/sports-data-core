using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class Aug20v4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Prediction",
                table: "ContestPreview",
                type: "character varying(768)",
                maxLength: 768,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Overview",
                table: "ContestPreview",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Analysis",
                table: "ContestPreview",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 20, 18, 47, 33, 713, DateTimeKind.Utc).AddTicks(7433), new DateTime(2025, 8, 20, 18, 47, 33, 713, DateTimeKind.Utc).AddTicks(7532) });

            migrationBuilder.CreateIndex(
                name: "IX_ContestPreview_ContestId",
                table: "ContestPreview",
                column: "ContestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContestPreview_ContestId",
                table: "ContestPreview");

            migrationBuilder.AlterColumn<string>(
                name: "Prediction",
                table: "ContestPreview",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(768)",
                oldMaxLength: 768,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Overview",
                table: "ContestPreview",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Analysis",
                table: "ContestPreview",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 20, 14, 11, 36, 848, DateTimeKind.Utc).AddTicks(4433), new DateTime(2025, 8, 20, 14, 11, 36, 848, DateTimeKind.Utc).AddTicks(4562) });
        }
    }
}
