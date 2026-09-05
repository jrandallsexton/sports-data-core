using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Notification.Migrations
{
    /// <inheritdoc />
    public partial class PickDeadlineClaimWaveAnchor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationPickDeadlines_UserId_LeagueId_SeasonWeek_FireTi~",
                table: "NotificationPickDeadlines");

            migrationBuilder.AddColumn<DateTime>(
                name: "WaveAnchorUtc",
                table: "NotificationPickDeadlines",
                type: "timestamp with time zone",
                nullable: true);

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
            // Claim rows are dispatch AUDIT history and are deliberately NOT
            // purged on rollback (unlike PendingScheduledJob schedule rows).
            // Recreating the narrower v1 index can only fail in the contrived
            // case this migration exists to prevent — two waves claimed on
            // the same FireTimeUtc across a lead-time change; resolve such a
            // duplicate manually before rolling back.
            migrationBuilder.DropIndex(
                name: "IX_NotificationPickDeadlines_UserId_LeagueId_SeasonWeek_FireTi~",
                table: "NotificationPickDeadlines");

            migrationBuilder.DropColumn(
                name: "WaveAnchorUtc",
                table: "NotificationPickDeadlines");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPickDeadlines_UserId_LeagueId_SeasonWeek_FireTi~",
                table: "NotificationPickDeadlines",
                columns: new[] { "UserId", "LeagueId", "SeasonWeek", "FireTimeUtc" },
                unique: true);
        }
    }
}
