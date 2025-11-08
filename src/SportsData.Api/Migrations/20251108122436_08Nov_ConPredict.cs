using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class _08Nov_ConPredict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContestPrediction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    WinnerFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    WinProbability = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    PredictionType = table.Column<int>(type: "integer", nullable: false),
                    ModelVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestPrediction", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContestPrediction_ContestId_PredictionType_ModelVersion",
                table: "ContestPrediction",
                columns: new[] { "ContestId", "PredictionType", "ModelVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContestPrediction_ModelVersion",
                table: "ContestPrediction",
                column: "ModelVersion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContestPrediction");
        }
    }
}
