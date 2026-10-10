using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _10OctV2_InstantAnalysisLab : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayAnalysisRating",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stars = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayAnalysisRating", x => x.Id);
                    table.CheckConstraint("CK_PlayAnalysisRating_Stars_Range", "\"Stars\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_PlayAnalysisRating_PlayAnalysis_PlayAnalysisId",
                        column: x => x.PlayAnalysisId,
                        principalTable: "PlayAnalysis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayAnalysisRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayCount = table.Column<int>(type: "integer", nullable: false),
                    FactCount = table.Column<int>(type: "integer", nullable: false),
                    DroppedCount = table.Column<int>(type: "integer", nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    DroppedFacts = table.Column<string>(type: "jsonb", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayAnalysisRun", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayAnalysisRating_PlayAnalysisId",
                table: "PlayAnalysisRating",
                column: "PlayAnalysisId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayAnalysisRun_ContestId",
                table: "PlayAnalysisRun",
                column: "ContestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayAnalysisRating");

            migrationBuilder.DropTable(
                name: "PlayAnalysisRun");
        }
    }
}
