using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class _24DecV2_DoubleRel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PickemGroupInvitations_PickemGroup_PickemGroupId1",
                table: "PickemGroupInvitations");

            migrationBuilder.DropIndex(
                name: "IX_PickemGroupInvitations_PickemGroupId1",
                table: "PickemGroupInvitations");

            migrationBuilder.DropColumn(
                name: "PickemGroupId1",
                table: "PickemGroupInvitations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PickemGroupId1",
                table: "PickemGroupInvitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickemGroupInvitations_PickemGroupId1",
                table: "PickemGroupInvitations",
                column: "PickemGroupId1");

            migrationBuilder.AddForeignKey(
                name: "FK_PickemGroupInvitations_PickemGroup_PickemGroupId1",
                table: "PickemGroupInvitations",
                column: "PickemGroupId1",
                principalTable: "PickemGroup",
                principalColumn: "Id");
        }
    }
}
