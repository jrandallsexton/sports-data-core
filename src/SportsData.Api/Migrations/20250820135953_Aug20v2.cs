using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class Aug20v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContestPreview",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Overview = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Analysis = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Prediction = table.Column<string>(type: "text", nullable: true),
                    PredictedStraightUpWinner = table.Column<Guid>(type: "uuid", nullable: true),
                    PredictedSpreadWinner = table.Column<Guid>(type: "uuid", nullable: true),
                    OverUnderPrediction = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestPreview", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 20, 13, 59, 53, 39, DateTimeKind.Utc).AddTicks(2771), new DateTime(2025, 8, 20, 13, 59, 53, 39, DateTimeKind.Utc).AddTicks(2865) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContestPreview");

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 20, 9, 58, 18, 909, DateTimeKind.Utc).AddTicks(294), new DateTime(2025, 8, 20, 9, 58, 18, 909, DateTimeKind.Utc).AddTicks(408) });
        }
    }
}
