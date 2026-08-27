using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class PickemGroupWeekSeasonPhase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeasonPhaseTypeCode",
                table: "PickemGroupWeek",
                type: "integer",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeasonPhaseTypeCode",
                table: "PickemGroupWeek");
        }
    }
}
