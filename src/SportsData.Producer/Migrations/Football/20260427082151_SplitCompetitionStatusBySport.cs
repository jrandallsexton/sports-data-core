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
            // Default to the concrete football subtype so the column
            // never carries an empty / unmapped TPH discriminator —
            // existing rows in this DB were all written by the
            // pre-split processor, which is now FootballCompetitionStatus.
            // The UPDATE backfill below is retained as a defensive
            // idempotent step in case any row somehow predates the
            // default being applied.
            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "CompetitionStatus",
                type: "character varying(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "FootballCompetitionStatus");

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
