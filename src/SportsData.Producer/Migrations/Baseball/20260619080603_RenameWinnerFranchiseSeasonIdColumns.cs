using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class RenameWinnerFranchiseSeasonIdColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WinnerFranchiseId",
                table: "Contest",
                newName: "WinnerFranchiseSeasonId");

            migrationBuilder.RenameColumn(
                name: "SpreadWinnerFranchiseId",
                table: "Contest",
                newName: "SpreadWinnerFranchiseSeasonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WinnerFranchiseSeasonId",
                table: "Contest",
                newName: "WinnerFranchiseId");

            migrationBuilder.RenameColumn(
                name: "SpreadWinnerFranchiseSeasonId",
                table: "Contest",
                newName: "SpreadWinnerFranchiseId");
        }
    }
}
