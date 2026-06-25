using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Notification.Migrations
{
    /// <inheritdoc />
    public partial class MakePendingScheduledJobIndexUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingScheduledJobs_UserId_JobKind_TargetId_SeasonWeek",
                table: "PendingScheduledJobs");

            migrationBuilder.CreateIndex(
                name: "IX_PendingScheduledJobs_UserId_JobKind_TargetId_SeasonWeek",
                table: "PendingScheduledJobs",
                columns: new[] { "UserId", "JobKind", "TargetId", "SeasonWeek" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingScheduledJobs_UserId_JobKind_TargetId_SeasonWeek",
                table: "PendingScheduledJobs");

            migrationBuilder.CreateIndex(
                name: "IX_PendingScheduledJobs_UserId_JobKind_TargetId_SeasonWeek",
                table: "PendingScheduledJobs",
                columns: new[] { "UserId", "JobKind", "TargetId", "SeasonWeek" });
        }
    }
}
