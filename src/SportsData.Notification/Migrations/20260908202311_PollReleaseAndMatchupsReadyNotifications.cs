using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Notification.Migrations
{
    /// <inheritdoc />
    public partial class PollReleaseAndMatchupsReadyNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MatchupsReadyEnabled",
                table: "UserNotificationPreferences",
                type: "boolean",
                nullable: false,
                // "Everything on" default (mirrors the entity initializer and
                // the other *Enabled flags) so existing preference rows opt in.
                // The scaffolded false silently opted out every user with a
                // pre-existing prefs row — caught in local E2E 2026-09-08.
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PollReleasedEnabled",
                table: "UserNotificationPreferences",
                type: "boolean",
                nullable: false,
                // Same "everything on" default as above.
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "NotificationMatchupsReady",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    SeasonWeek = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Body = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Result = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AttemptedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationMatchupsReady", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPollReleases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonPollWeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonPollId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Body = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Result = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AttemptedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPollReleases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMatchupsReady_UserId_LeagueId_SeasonYear_Season~",
                table: "NotificationMatchupsReady",
                columns: new[] { "UserId", "LeagueId", "SeasonYear", "SeasonWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPollReleases_UserId_SeasonPollWeekId",
                table: "NotificationPollReleases",
                columns: new[] { "UserId", "SeasonPollWeekId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationMatchupsReady");

            migrationBuilder.DropTable(
                name: "NotificationPollReleases");

            migrationBuilder.DropColumn(
                name: "MatchupsReadyEnabled",
                table: "UserNotificationPreferences");

            migrationBuilder.DropColumn(
                name: "PollReleasedEnabled",
                table: "UserNotificationPreferences");
        }
    }
}
