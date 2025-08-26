using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class _26Aug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwayScore",
                table: "MatchupPreview",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeScore",
                table: "MatchupPreview",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "MatchupPreview",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwayScore",
                table: "MatchupPreview");

            migrationBuilder.DropColumn(
                name: "HomeScore",
                table: "MatchupPreview");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "MatchupPreview");
        }
    }
}
