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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingScheduledJobs_UserId_JobKind_TargetId_SeasonWeek_Wav~",
                table: "PendingScheduledJobs");

            migrationBuilder.DropColumn(
                name: "Headline",
                table: "PickemGroupMatchups");

            migrationBuilder.DropColumn(
                name: "WaveAnchorUtc",
                table: "PendingScheduledJobs");

            migrationBuilder.CreateIndex(
                name: "IX_PendingScheduledJobs_UserId_JobKind_TargetId_SeasonWeek",
                table: "PendingScheduledJobs",
                columns: new[] { "UserId", "JobKind", "TargetId", "SeasonWeek" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
