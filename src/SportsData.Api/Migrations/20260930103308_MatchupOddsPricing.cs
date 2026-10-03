using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class MatchupOddsPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwayMoneyLine",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AwaySpreadPrice",
                table: "PickemGroupMatchup",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeMoneyLine",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HomeSpreadPrice",
                table: "PickemGroupMatchup",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwayMoneyLine",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "AwaySpreadPrice",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "HomeMoneyLine",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "HomeSpreadPrice",
                table: "PickemGroupMatchup");
        }
    }
}
