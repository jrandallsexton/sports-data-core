using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _22SepV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionLeaderStat_Athlete_AthleteSeasonId",
                table: "CompetitionLeaderStat");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionLeaderStat_AthleteSeason_AthleteSeasonId",
                table: "CompetitionLeaderStat",
                column: "AthleteSeasonId",
                principalTable: "AthleteSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionLeaderStat_AthleteSeason_AthleteSeasonId",
                table: "CompetitionLeaderStat");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionLeaderStat_Athlete_AthleteSeasonId",
                table: "CompetitionLeaderStat",
                column: "AthleteSeasonId",
                principalTable: "Athlete",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
