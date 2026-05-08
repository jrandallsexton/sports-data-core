using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class SplitCompetitionCompetitorBySport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CuratedRankCurrent",
                table: "CompetitionCompetitor");

            // Each DB is single-sport, so existing rows in the baseball
            // CompetitionCompetitor table all belong to the baseball subtype.
            // Hardcoded default backfills existing rows; EF supplies the value
            // for inserts going forward.
            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "CompetitionCompetitor",
                type: "character varying(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "BaseballCompetitionCompetitor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "CompetitionCompetitor");

            migrationBuilder.AddColumn<int>(
                name: "CuratedRankCurrent",
                table: "CompetitionCompetitor",
                type: "integer",
                nullable: true);
        }
    }
}
