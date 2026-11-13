using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _12NovV2_FranchiseContestFact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FranchiseContestFact",
                columns: table => new
                {
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpponentFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpponentFranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    Week = table.Column<int>(type: "integer", nullable: true),
                    SeasonPhaseTypeCode = table.Column<int>(type: "integer", nullable: true),
                    KickoffUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KickoffTimeKnown = table.Column<bool>(type: "boolean", nullable: false),
                    KickoffLocal = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    KickoffWindow = table.Column<int>(type: "integer", nullable: true),
                    LocalMonth = table.Column<int>(type: "integer", nullable: true),
                    LocalDayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    Site = table.Column<int>(type: "integer", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: true),
                    Conference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OpponentConference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsConferenceGame = table.Column<bool>(type: "boolean", nullable: false),
                    Division = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OpponentDivision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RankGoingIn = table.Column<int>(type: "integer", nullable: true),
                    OpponentRankGoingIn = table.Column<int>(type: "integer", nullable: true),
                    WinsBefore = table.Column<int>(type: "integer", nullable: false),
                    LossesBefore = table.Column<int>(type: "integer", nullable: false),
                    TiesBefore = table.Column<int>(type: "integer", nullable: false),
                    OpponentWinsBefore = table.Column<int>(type: "integer", nullable: false),
                    OpponentLossesBefore = table.Column<int>(type: "integer", nullable: false),
                    OpponentTiesBefore = table.Column<int>(type: "integer", nullable: false),
                    PointsFor = table.Column<int>(type: "integer", nullable: true),
                    PointsAgainst = table.Column<int>(type: "integer", nullable: true),
                    Margin = table.Column<int>(type: "integer", nullable: true),
                    Result = table.Column<int>(type: "integer", nullable: true),
                    Spread = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    OverUnder = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    AtsResult = table.Column<int>(type: "integer", nullable: true),
                    TotalResult = table.Column<int>(type: "integer", nullable: true),
                    TotalYards = table.Column<int>(type: "integer", nullable: true),
                    PassingYards = table.Column<int>(type: "integer", nullable: true),
                    RushingYards = table.Column<int>(type: "integer", nullable: true),
                    PassAttempts = table.Column<int>(type: "integer", nullable: true),
                    Completions = table.Column<int>(type: "integer", nullable: true),
                    RushAttempts = table.Column<int>(type: "integer", nullable: true),
                    TotalPlays = table.Column<int>(type: "integer", nullable: true),
                    FirstDowns = table.Column<int>(type: "integer", nullable: true),
                    ThirdDownConversions = table.Column<int>(type: "integer", nullable: true),
                    ThirdDownAttempts = table.Column<int>(type: "integer", nullable: true),
                    FourthDownConversions = table.Column<int>(type: "integer", nullable: true),
                    FourthDownAttempts = table.Column<int>(type: "integer", nullable: true),
                    RedZoneAttempts = table.Column<int>(type: "integer", nullable: true),
                    RedZoneScores = table.Column<int>(type: "integer", nullable: true),
                    Giveaways = table.Column<int>(type: "integer", nullable: true),
                    Takeaways = table.Column<int>(type: "integer", nullable: true),
                    Penalties = table.Column<int>(type: "integer", nullable: true),
                    PenaltyYards = table.Column<int>(type: "integer", nullable: true),
                    PossessionSeconds = table.Column<int>(type: "integer", nullable: true),
                    SacksMade = table.Column<int>(type: "integer", nullable: true),
                    SacksAllowed = table.Column<int>(type: "integer", nullable: true),
                    ProjectionVersion = table.Column<int>(type: "integer", nullable: false),
                    ProjectedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranchiseContestFact", x => new { x.ContestId, x.FranchiseSeasonId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseContestFact_FranchiseId_SeasonYear",
                table: "FranchiseContestFact",
                columns: new[] { "FranchiseId", "SeasonYear" });

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseContestFact_OpponentFranchiseId_SeasonYear",
                table: "FranchiseContestFact",
                columns: new[] { "OpponentFranchiseId", "SeasonYear" });

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseContestFact_SeasonYear",
                table: "FranchiseContestFact",
                column: "SeasonYear");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FranchiseContestFact");
        }
    }
}
