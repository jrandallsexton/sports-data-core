using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameUserPickFranchiseIdToFranchiseSeasonId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FranchiseId",
                table: "UserPick",
                newName: "FranchiseSeasonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FranchiseSeasonId",
                table: "UserPick",
                newName: "FranchiseId");
        }
    }
}
