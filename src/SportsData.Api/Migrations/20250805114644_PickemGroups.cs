using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class PickemGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanonicalId",
                table: "User");

            migrationBuilder.AlterColumn<string>(
                name: "Timezone",
                table: "User",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SignInProvider",
                table: "User",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FirebaseUid",
                table: "User",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "User",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Contest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonWeek = table.Column<int>(type: "integer", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HomeFranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwayFranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Spread = table.Column<double>(type: "double precision", nullable: true),
                    OverUnder = table.Column<double>(type: "double precision", nullable: true),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HomeScore = table.Column<int>(type: "integer", nullable: true),
                    AwayScore = table.Column<int>(type: "integer", nullable: true),
                    WinnerFranchiseId = table.Column<Guid>(type: "uuid", nullable: true),
                    SpreadWinnerFranchiseId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinalizedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contest", x => x.Id);
                    table.UniqueConstraint("AK_Contest_ContestId", x => x.ContestId);
                });

            migrationBuilder.CreateTable(
                name: "ContestResult",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HomeFranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwayFranchiseId = table.Column<Guid>(type: "uuid", nullable: false),
                    WinningFranchiseId = table.Column<Guid>(type: "uuid", nullable: true),
                    HomeScore = table.Column<int>(type: "integer", nullable: false),
                    AwayScore = table.Column<int>(type: "integer", nullable: false),
                    OverUnder = table.Column<double>(type: "double precision", nullable: true),
                    Spread = table.Column<double>(type: "double precision", nullable: true),
                    WasCanceled = table.Column<bool>(type: "boolean", nullable: false),
                    WentToOvertime = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestResult", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeagueStandingHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonWeek = table.Column<int>(type: "integer", nullable: false),
                    TotalPoints = table.Column<int>(type: "integer", nullable: false),
                    CorrectPicks = table.Column<int>(type: "integer", nullable: false),
                    TotalPicks = table.Column<int>(type: "integer", nullable: false),
                    WeeksWon = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    CalculatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueStandingHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeagueWeekResult",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonWeek = table.Column<int>(type: "integer", nullable: false),
                    TotalPoints = table.Column<int>(type: "integer", nullable: false),
                    CorrectPicks = table.Column<int>(type: "integer", nullable: false),
                    TotalPicks = table.Column<int>(type: "integer", nullable: false),
                    IsWeeklyWinner = table.Column<bool>(type: "boolean", nullable: false),
                    CalculatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueWeekResult", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PickemGroup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    League = table.Column<int>(type: "integer", nullable: false),
                    PickType = table.Column<int>(type: "integer", nullable: false),
                    TiebreakerType = table.Column<int>(type: "integer", nullable: false),
                    TiebreakerTiePolicy = table.Column<int>(type: "integer", nullable: false),
                    UseConfidencePoints = table.Column<bool>(type: "boolean", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    CommissionerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickemGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PickemGroupContest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonWeek = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickemGroupContest", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PickemGroupUser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickemGroupUser", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PickResult",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserPickId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    PointsAwarded = table.Column<int>(type: "integer", nullable: false),
                    RuleVersion = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickResult", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPick",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseId = table.Column<Guid>(type: "uuid", nullable: true),
                    OverUnder = table.Column<int>(type: "integer", nullable: true),
                    ConfidencePoints = table.Column<int>(type: "integer", nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    PointsAwarded = table.Column<int>(type: "integer", nullable: true),
                    WasAgainstSpread = table.Column<bool>(type: "boolean", nullable: true),
                    ScoredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TiebreakerType = table.Column<int>(type: "integer", nullable: false),
                    TiebreakerGuessTotal = table.Column<int>(type: "integer", nullable: true),
                    TiebreakerGuessHome = table.Column<int>(type: "integer", nullable: true),
                    TiebreakerGuessAway = table.Column<int>(type: "integer", nullable: true),
                    TiebreakerActualTotal = table.Column<int>(type: "integer", nullable: true),
                    TiebreakerActualHome = table.Column<int>(type: "integer", nullable: true),
                    TiebreakerActualAway = table.Column<int>(type: "integer", nullable: true),
                    ImportedFromPickId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPick", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPick_Contest_ContestId",
                        column: x => x.ContestId,
                        principalTable: "Contest",
                        principalColumn: "ContestId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PickemGroupConference",
                columns: table => new
                {
                    PickemGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickemGroupConference", x => new { x.PickemGroupId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_PickemGroupConference_PickemGroup_GroupId",
                        column: x => x.GroupId,
                        principalTable: "PickemGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PickemGroupConference_PickemGroup_PickemGroupId",
                        column: x => x.PickemGroupId,
                        principalTable: "PickemGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedUtc", "LastLoginUtc" },
                values: new object[] { new DateTime(2025, 8, 5, 11, 46, 43, 592, DateTimeKind.Utc).AddTicks(4399), new DateTime(2025, 8, 5, 11, 46, 43, 592, DateTimeKind.Utc).AddTicks(4515) });

            migrationBuilder.CreateIndex(
                name: "IX_Contest_ContestId",
                table: "Contest",
                column: "ContestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contest_Sport_SeasonYear_StartUtc",
                table: "Contest",
                columns: new[] { "Sport", "SeasonYear", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ContestResult_ContestId",
                table: "ContestResult",
                column: "ContestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContestResult_Sport_SeasonYear",
                table: "ContestResult",
                columns: new[] { "Sport", "SeasonYear" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueStandingHistory_PickemGroupId_UserId_SeasonYear_Seaso~",
                table: "LeagueStandingHistory",
                columns: new[] { "PickemGroupId", "UserId", "SeasonYear", "SeasonWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueWeekResult_PickemGroupId_SeasonYear_SeasonWeek_UserId",
                table: "LeagueWeekResult",
                columns: new[] { "PickemGroupId", "SeasonYear", "SeasonWeek", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroup_CommissionerUserId",
                table: "PickemGroup",
                column: "CommissionerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupConference_GroupId",
                table: "PickemGroupConference",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupContest_PickemGroupId_ContestId",
                table: "PickemGroupContest",
                columns: new[] { "PickemGroupId", "ContestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupContest_PickemGroupId_SeasonYear_SeasonWeek",
                table: "PickemGroupContest",
                columns: new[] { "PickemGroupId", "SeasonYear", "SeasonWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupUser_PickemGroupId_UserId",
                table: "PickemGroupUser",
                columns: new[] { "PickemGroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickResult_UserPickId",
                table: "PickResult",
                column: "UserPickId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPick_ContestId",
                table: "UserPick",
                column: "ContestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPick_PickemGroupId_UserId_ContestId",
                table: "UserPick",
                columns: new[] { "PickemGroupId", "UserId", "ContestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContestResult");

            migrationBuilder.DropTable(
                name: "LeagueStandingHistory");

            migrationBuilder.DropTable(
                name: "LeagueWeekResult");

            migrationBuilder.DropTable(
                name: "PickemGroupConference");

            migrationBuilder.DropTable(
                name: "PickemGroupContest");

            migrationBuilder.DropTable(
                name: "PickemGroupUser");

            migrationBuilder.DropTable(
                name: "PickResult");

            migrationBuilder.DropTable(
                name: "UserPick");

            migrationBuilder.DropTable(
                name: "PickemGroup");

            migrationBuilder.DropTable(
                name: "Contest");

            migrationBuilder.AlterColumn<string>(
                name: "Timezone",
                table: "User",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SignInProvider",
                table: "User",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "FirebaseUid",
                table: "User",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "User",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CanonicalId",
                table: "User",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CanonicalId", "CreatedUtc", "LastLoginUtc" },
                values: new object[] { null, new DateTime(2025, 5, 15, 9, 14, 37, 276, DateTimeKind.Utc).AddTicks(5428), new DateTime(2025, 5, 15, 9, 14, 37, 276, DateTimeKind.Utc).AddTicks(5557) });
        }
    }
}
