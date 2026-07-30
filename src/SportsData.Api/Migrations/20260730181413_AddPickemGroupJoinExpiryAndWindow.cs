using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPickemGroupJoinExpiryAndWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InvitationsExpireUtc",
                table: "PickemGroup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeagueWindow",
                table: "PickemGroup",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill: windowed rows become DateRange (2). Exact, not
            // heuristic -- WeekRange has never been submittable (the create
            // UI blocks it), so every pre-existing row with an authored bound
            // is a DateRange league and every other row is FullSeason (the
            // column default). InvitationsExpireUtc needs no backfill: the
            // hourly LeagueJoinExpiryAuditJob sweep computes it for every
            // active league.
            migrationBuilder.Sql(
                @"UPDATE ""PickemGroup""
                  SET ""LeagueWindow"" = 2
                  WHERE ""EndsOn"" IS NOT NULL OR ""StartsOn"" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvitationsExpireUtc",
                table: "PickemGroup");

            migrationBuilder.DropColumn(
                name: "LeagueWindow",
                table: "PickemGroup");
        }
    }
}
