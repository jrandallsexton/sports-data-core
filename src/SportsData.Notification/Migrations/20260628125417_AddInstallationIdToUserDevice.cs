using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Notification.Migrations
{
    /// <inheritdoc />
    public partial class AddInstallationIdToUserDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // UserDevices is a small, self-rebuilding projection: every device
            // re-registers (with its InstallationId) on the next app launch /
            // token refresh. Existing rows predate InstallationId and can't be
            // backfilled with a real value, so clearing them is the only way to
            // add a required, uniquely-indexed column without colliding on "".
            migrationBuilder.Sql("DELETE FROM \"UserDevices\";");

            migrationBuilder.DropIndex(
                name: "IX_UserDevices_UserId_FcmToken",
                table: "UserDevices");

            // No defaultValue: the DELETE above empties the table, so the NOT NULL
            // column adds cleanly without one. Omitting it means any future insert
            // that fails to supply InstallationId errors loudly (NOT NULL) instead
            // of silently landing an empty string on the unique natural key.
            migrationBuilder.AddColumn<string>(
                name: "InstallationId",
                table: "UserDevices",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_InstallationId",
                table: "UserDevices",
                column: "InstallationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_UserId",
                table: "UserDevices",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserDevices_InstallationId",
                table: "UserDevices");

            migrationBuilder.DropIndex(
                name: "IX_UserDevices_UserId",
                table: "UserDevices");

            migrationBuilder.DropColumn(
                name: "InstallationId",
                table: "UserDevices");

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_UserId_FcmToken",
                table: "UserDevices",
                columns: new[] { "UserId", "FcmToken" },
                unique: true);
        }
    }
}
