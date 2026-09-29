using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class CaptureScoresAndOverUnder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwayScore",
                table: "MatchupPreviewPrompt",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeScore",
                table: "MatchupPreviewPrompt",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OverUnderPrediction",
                table: "MatchupPreviewPrompt",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwayScore",
                table: "MatchupPreviewPrompt");

            migrationBuilder.DropColumn(
                name: "HomeScore",
                table: "MatchupPreviewPrompt");

            migrationBuilder.DropColumn(
                name: "OverUnderPrediction",
                table: "MatchupPreviewPrompt");
        }
    }
}
