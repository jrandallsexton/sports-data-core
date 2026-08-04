using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPickemGroupInvitationInvitee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedUtc",
                table: "PickemGroupInvitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeclinedUtc",
                table: "PickemGroupInvitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InviteeUserId",
                table: "PickemGroupInvitations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupInvitations_InviteeUserId",
                table: "PickemGroupInvitations",
                column: "InviteeUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PickemGroupInvitations_User_InviteeUserId",
                table: "PickemGroupInvitations",
                column: "InviteeUserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PickemGroupInvitations_User_InviteeUserId",
                table: "PickemGroupInvitations");

            migrationBuilder.DropIndex(
                name: "IX_PickemGroupInvitations_InviteeUserId",
                table: "PickemGroupInvitations");

            migrationBuilder.DropColumn(
                name: "AcceptedUtc",
                table: "PickemGroupInvitations");

            migrationBuilder.DropColumn(
                name: "DeclinedUtc",
                table: "PickemGroupInvitations");

            migrationBuilder.DropColumn(
                name: "InviteeUserId",
                table: "PickemGroupInvitations");
        }
    }
}
