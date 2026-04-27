using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class SplitCompetitionStatusBySport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "CompetitionStatus",
                type: "character varying(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "");

            // Existing rows in the football DB are all the football
            // subtype — backfill the discriminator so EF's typed
            // queries (Set<FootballCompetitionStatus>) match them.
            // Without this every pre-migration row reads as the empty
            // default and is invisible to the new code path.
            migrationBuilder.Sql(
                "UPDATE \"CompetitionStatus\" SET \"Discriminator\" = 'FootballCompetitionStatus' WHERE \"Discriminator\" = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "CompetitionStatus");
        }
    }
}
