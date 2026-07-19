using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class _19JulV1_PickemGroupSeasonYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeasonYear",
                table: "PickemGroup",
                type: "integer",
                nullable: false,
                defaultValue: 2026);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeasonYear",
                table: "PickemGroup");
        }
    }
}
