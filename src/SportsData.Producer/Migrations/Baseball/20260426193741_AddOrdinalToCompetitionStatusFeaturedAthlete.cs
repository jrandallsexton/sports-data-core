using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class AddOrdinalToCompetitionStatusFeaturedAthlete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Ordinal",
                table: "CompetitionStatusFeaturedAthlete",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ordinal",
                table: "CompetitionStatusFeaturedAthlete");
        }
    }
}
