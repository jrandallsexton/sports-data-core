using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _01SepV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConferenceLosses",
                table: "FranchiseSeason",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConferenceTies",
                table: "FranchiseSeason",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConferenceWins",
                table: "FranchiseSeason",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConferenceLosses",
                table: "FranchiseSeason");

            migrationBuilder.DropColumn(
                name: "ConferenceTies",
                table: "FranchiseSeason");

            migrationBuilder.DropColumn(
                name: "ConferenceWins",
                table: "FranchiseSeason");
        }
    }
}
