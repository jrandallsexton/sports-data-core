using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingInvitationUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PickemGroupInvitations_PickemGroupId",
                table: "PickemGroupInvitations");

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupInvitations_PickemGroupId_InviteeUserId",
                table: "PickemGroupInvitations",
                columns: new[] { "PickemGroupId", "InviteeUserId" },
                unique: true,
                filter: "\"AcceptedUtc\" IS NULL AND \"DeclinedUtc\" IS NULL AND NOT \"IsRevoked\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PickemGroupInvitations_PickemGroupId_InviteeUserId",
                table: "PickemGroupInvitations");

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupInvitations_PickemGroupId",
                table: "PickemGroupInvitations",
                column: "PickemGroupId");
        }
    }
}
