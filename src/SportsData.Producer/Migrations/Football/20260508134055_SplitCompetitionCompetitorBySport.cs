using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class SplitCompetitionCompetitorBySport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Each DB is single-sport, so existing rows in the football
            // CompetitionCompetitor table all belong to the football subtype.
            // Hardcoded default backfills existing rows; EF supplies the value
            // for inserts going forward.
            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "CompetitionCompetitor",
                type: "character varying(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "FootballCompetitionCompetitor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "CompetitionCompetitor");
        }
    }
}
