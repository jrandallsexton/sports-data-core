using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Notification.Migrations
{
    /// <inheritdoc />
    public partial class AddOddsChangedNotificationPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OddsChangedEnabled",
                table: "UserNotificationPreferences",
                type: "boolean",
                nullable: false,
                // "Everything on" default (mirrors the entity initializer and
                // the other *Enabled flags) so existing preference rows opt in
                // to line-move pushes rather than being silently excluded.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OddsChangedEnabled",
                table: "UserNotificationPreferences");
        }
    }
}
