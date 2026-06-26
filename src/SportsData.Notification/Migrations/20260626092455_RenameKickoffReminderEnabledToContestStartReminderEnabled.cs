using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Notification.Migrations
{
    /// <inheritdoc />
    public partial class RenameKickoffReminderEnabledToContestStartReminderEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "KickoffReminderEnabled",
                table: "UserNotificationPreferences",
                newName: "ContestStartReminderEnabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ContestStartReminderEnabled",
                table: "UserNotificationPreferences",
                newName: "KickoffReminderEnabled");
        }
    }
}
