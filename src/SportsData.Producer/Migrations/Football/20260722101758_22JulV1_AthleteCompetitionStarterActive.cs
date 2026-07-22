using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _22JulV1_AthleteCompetitionStarterActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "AthleteCompetition",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Starter",
                table: "AthleteCompetition",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Active",
                table: "AthleteCompetition");

            migrationBuilder.DropColumn(
                name: "Starter",
                table: "AthleteCompetition");
        }
    }
}
