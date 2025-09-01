using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class MatchupRecordSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwayConferenceLosses",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AwayConferenceTies",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AwayConferenceWins",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AwayLosses",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AwayTies",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AwayWins",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HomeConferenceLosses",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HomeConferenceTies",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HomeConferenceWins",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HomeLosses",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HomeTies",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HomeWins",
                table: "PickemGroupMatchup",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPick_PickemGroup_PickemGroupId",
                table: "UserPick",
                column: "PickemGroupId",
                principalTable: "PickemGroup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPick_PickemGroup_PickemGroupId",
                table: "UserPick");

            migrationBuilder.DropColumn(
                name: "AwayConferenceLosses",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "AwayConferenceTies",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "AwayConferenceWins",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "AwayLosses",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "AwayTies",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "AwayWins",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "HomeConferenceLosses",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "HomeConferenceTies",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "HomeConferenceWins",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "HomeLosses",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "HomeTies",
                table: "PickemGroupMatchup");

            migrationBuilder.DropColumn(
                name: "HomeWins",
                table: "PickemGroupMatchup");
        }
    }
}
