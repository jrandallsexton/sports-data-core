using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class _21JulV1_CompetitionCompetitorRecordFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Abbreviation",
                table: "CompetitionCompetitorRecord",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "CompetitionCompetitorRecord",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "CompetitionCompetitorRecord",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortDisplayName",
                table: "CompetitionCompetitorRecord",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Abbreviation",
                table: "CompetitionCompetitorRecord");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "CompetitionCompetitorRecord");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "CompetitionCompetitorRecord");

            migrationBuilder.DropColumn(
                name: "ShortDisplayName",
                table: "CompetitionCompetitorRecord");
        }
    }
}
