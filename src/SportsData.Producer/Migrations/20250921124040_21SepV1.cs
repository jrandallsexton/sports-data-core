using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _21SepV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionLeaderStat_Athlete_AthleteId",
                table: "CompetitionLeaderStat");

            migrationBuilder.RenameColumn(
                name: "AthleteId",
                table: "CompetitionLeaderStat",
                newName: "AthleteSeasonId");

            migrationBuilder.RenameIndex(
                name: "IX_CompetitionLeaderStat_AthleteId",
                table: "CompetitionLeaderStat",
                newName: "IX_CompetitionLeaderStat_AthleteSeasonId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionLeaderStat_Athlete_AthleteSeasonId",
                table: "CompetitionLeaderStat",
                column: "AthleteSeasonId",
                principalTable: "Athlete",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionLeaderStat_Athlete_AthleteSeasonId",
                table: "CompetitionLeaderStat");

            migrationBuilder.RenameColumn(
                name: "AthleteSeasonId",
                table: "CompetitionLeaderStat",
                newName: "AthleteId");

            migrationBuilder.RenameIndex(
                name: "IX_CompetitionLeaderStat_AthleteSeasonId",
                table: "CompetitionLeaderStat",
                newName: "IX_CompetitionLeaderStat_AthleteId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionLeaderStat_Athlete_AthleteId",
                table: "CompetitionLeaderStat",
                column: "AthleteId",
                principalTable: "Athlete",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
