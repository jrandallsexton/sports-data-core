using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class FuckingRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Competition_Venue_VenueId",
                table: "Competition");

            migrationBuilder.CreateIndex(
                name: "IX_Contest_AwayTeamFranchiseSeasonId",
                table: "Contest",
                column: "AwayTeamFranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Contest_HomeTeamFranchiseSeasonId",
                table: "Contest",
                column: "HomeTeamFranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Contest_VenueId",
                table: "Contest",
                column: "VenueId");

            migrationBuilder.AddForeignKey(
                name: "FK_Competition_Venue_VenueId",
                table: "Competition",
                column: "VenueId",
                principalTable: "Venue",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contest_FranchiseSeason_AwayTeamFranchiseSeasonId",
                table: "Contest",
                column: "AwayTeamFranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contest_FranchiseSeason_HomeTeamFranchiseSeasonId",
                table: "Contest",
                column: "HomeTeamFranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contest_Venue_VenueId",
                table: "Contest",
                column: "VenueId",
                principalTable: "Venue",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Competition_Venue_VenueId",
                table: "Competition");

            migrationBuilder.DropForeignKey(
                name: "FK_Contest_FranchiseSeason_AwayTeamFranchiseSeasonId",
                table: "Contest");

            migrationBuilder.DropForeignKey(
                name: "FK_Contest_FranchiseSeason_HomeTeamFranchiseSeasonId",
                table: "Contest");

            migrationBuilder.DropForeignKey(
                name: "FK_Contest_Venue_VenueId",
                table: "Contest");

            migrationBuilder.DropIndex(
                name: "IX_Contest_AwayTeamFranchiseSeasonId",
                table: "Contest");

            migrationBuilder.DropIndex(
                name: "IX_Contest_HomeTeamFranchiseSeasonId",
                table: "Contest");

            migrationBuilder.DropIndex(
                name: "IX_Contest_VenueId",
                table: "Contest");

            migrationBuilder.AddForeignKey(
                name: "FK_Competition_Venue_VenueId",
                table: "Competition",
                column: "VenueId",
                principalTable: "Venue",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
