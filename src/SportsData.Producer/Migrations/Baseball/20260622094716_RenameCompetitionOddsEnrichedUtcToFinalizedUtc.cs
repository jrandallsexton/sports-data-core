using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class RenameCompetitionOddsEnrichedUtcToFinalizedUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EnrichedUtc",
                table: "CompetitionOdds",
                newName: "FinalizedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FinalizedUtc",
                table: "CompetitionOdds",
                newName: "EnrichedUtc");
        }
    }
}
