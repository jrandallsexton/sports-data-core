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

            migrationBuilder.AddColumn<string>(
                name: "InstallationId",
                table: "UserDevices",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_InstallationId",
                table: "UserDevices",
                column: "InstallationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserDevices_InstallationId",
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
