using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Notification.Migrations
{
    /// <inheritdoc />
    public partial class PickDeadlineWaveModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingScheduledJobs_UserId_JobKind_TargetId_SeasonWeek",
                table: "PendingScheduledJobs");

            migrationBuilder.DropIndex(
                name: "IX_NotificationPickDeadlines_UserId_LeagueId_SeasonWeek_FireTi~",
                table: "NotificationPickDeadlines");

            migrationBuilder.AddColumn<string>(
                name: "Headline",
                table: "PickemGroupMatchups",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WaveAnchorUtc",
                table: "PendingScheduledJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WaveAnchorUtc",
                table: "NotificationPickDeadlines",
                type: "timestamp with time zone",
                nullable: true);

            // One-time cleanup: ALL pre-refactor scheduled-job rows go.
            // PickDeadline rows describe a schedule shape that no longer
            // exists (one row per league-week, null anchor); ContestStart
            // rows point at Hangfire jobs bound to the deleted
            // NotificationDispatcher. The deploy runbook bulk-deletes the
            // orphaned Hangfire Scheduled jobs, then the admin backfill
            // rebuilds every reminder (v2 waves + contest-start) against the
            // slice handlers. See docs/features/pick-deadline-reminders-v2.md.
            migrationBuilder.Sql(
                "DELETE FROM \"PendingScheduledJobs\";");

            migrationBuilder.CreateIndex(
                name: "IX_PendingScheduledJobs_UserId_JobKind_TargetId_SeasonWeek_Wav~",
                table: "PendingScheduledJobs",
                columns: new[] { "UserId", "JobKind", "TargetId", "SeasonWeek", "WaveAnchorUtc" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPickDeadlines_UserId_LeagueId_SeasonWeek_FireTi~",
                table: "NotificationPickDeadlines",
                columns: new[] { "UserId", "LeagueId", "SeasonWeek", "FireTimeUtc", "WaveAnchorUtc" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mirror of Up's purge: v2 holds multiple wave rows per
            // (user, league, week) that differ only by WaveAnchorUtc —
            // recreating the v1 unique index over them would fail. Rolling
            // back forfeits the schedule rows either way (their Hangfire
            // jobs target the v2 slice handlers); the v1 image's steady-state
            // events rebuild its week-level schedule.
            //
            // NotificationPickDeadline claim rows are dispatch AUDIT history
            // and are deliberately NOT purged. Recreating their narrower v1
            // index can only fail in the contrived case the wider key exists
            // to prevent — two waves claimed on the same FireTimeUtc across
            // a lead-time change; resolve such a duplicate manually before
            // rolling back.
            migrationBuilder.Sql(
                "DELETE FROM \"PendingScheduledJobs\" WHERE \"JobKind\" = 'PickDeadline';");

            migrationBuilder.DropIndex(
                name: "IX_PendingScheduledJobs_UserId_JobKind_TargetId_SeasonWeek_Wav~",
                table: "PendingScheduledJobs");

            migrationBuilder.DropIndex(
                name: "IX_NotificationPickDeadlines_UserId_LeagueId_SeasonWeek_FireTi~",
                table: "NotificationPickDeadlines");

            migrationBuilder.DropColumn(
                name: "Headline",
                table: "PickemGroupMatchups");

            migrationBuilder.DropColumn(
                name: "WaveAnchorUtc",
                table: "PendingScheduledJobs");

            migrationBuilder.DropColumn(
                name: "WaveAnchorUtc",
                table: "NotificationPickDeadlines");

            migrationBuilder.CreateIndex(
                name: "IX_PendingScheduledJobs_UserId_JobKind_TargetId_SeasonWeek",
                table: "PendingScheduledJobs",
                columns: new[] { "UserId", "JobKind", "TargetId", "SeasonWeek" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPickDeadlines_UserId_LeagueId_SeasonWeek_FireTi~",
                table: "NotificationPickDeadlines",
                columns: new[] { "UserId", "LeagueId", "SeasonWeek", "FireTimeUtc" },
                unique: true);
        }
    }
}
