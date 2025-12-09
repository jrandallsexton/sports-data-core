using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class _08DecV1_WeeklyComputes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LeagueWeekResult",
                table: "LeagueWeekResult");

            migrationBuilder.RenameTable(
                name: "LeagueWeekResult",
                newName: "PickemGroupWeekResult");

            migrationBuilder.RenameIndex(
                name: "IX_LeagueWeekResult_PickemGroupId_SeasonYear_SeasonWeek_UserId",
                table: "PickemGroupWeekResult",
                newName: "IX_PickemGroupWeekResult_PickemGroupId_SeasonYear_SeasonWeek_U~");

            migrationBuilder.AddColumn<bool>(
                name: "IsDropWeek",
                table: "PickemGroupWeekResult",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Rank",
                table: "PickemGroupWeekResult",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PickemGroupWeekResult",
                table: "PickemGroupWeekResult",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupWeekResult_UserId",
                table: "PickemGroupWeekResult",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PickemGroupWeekResult_PickemGroup_PickemGroupId",
                table: "PickemGroupWeekResult",
                column: "PickemGroupId",
                principalTable: "PickemGroup",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PickemGroupWeekResult_User_UserId",
                table: "PickemGroupWeekResult",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PickemGroupWeekResult_PickemGroup_PickemGroupId",
                table: "PickemGroupWeekResult");

            migrationBuilder.DropForeignKey(
                name: "FK_PickemGroupWeekResult_User_UserId",
                table: "PickemGroupWeekResult");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PickemGroupWeekResult",
                table: "PickemGroupWeekResult");

            migrationBuilder.DropIndex(
                name: "IX_PickemGroupWeekResult_UserId",
                table: "PickemGroupWeekResult");

            migrationBuilder.DropColumn(
                name: "IsDropWeek",
                table: "PickemGroupWeekResult");

            migrationBuilder.DropColumn(
                name: "Rank",
                table: "PickemGroupWeekResult");

            migrationBuilder.RenameTable(
                name: "PickemGroupWeekResult",
                newName: "LeagueWeekResult");

            migrationBuilder.RenameIndex(
                name: "IX_PickemGroupWeekResult_PickemGroupId_SeasonYear_SeasonWeek_U~",
                table: "LeagueWeekResult",
                newName: "IX_LeagueWeekResult_PickemGroupId_SeasonYear_SeasonWeek_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LeagueWeekResult",
                table: "LeagueWeekResult",
                column: "Id");
        }
    }
}
