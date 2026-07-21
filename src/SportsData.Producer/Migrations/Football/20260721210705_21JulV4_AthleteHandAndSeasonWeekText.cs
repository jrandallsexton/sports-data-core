using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _21JulV4_AthleteHandAndSeasonWeekText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Text",
                table: "SeasonWeek",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandAbbreviation",
                table: "Athlete",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandDisplayValue",
                table: "Athlete",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandType",
                table: "Athlete",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Text",
                table: "SeasonWeek");

            migrationBuilder.DropColumn(
                name: "HandAbbreviation",
                table: "Athlete");

            migrationBuilder.DropColumn(
                name: "HandDisplayValue",
                table: "Athlete");

            migrationBuilder.DropColumn(
                name: "HandType",
                table: "Athlete");
        }
    }
}
