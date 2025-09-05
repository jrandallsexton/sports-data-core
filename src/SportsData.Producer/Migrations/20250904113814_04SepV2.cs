using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _04SepV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SeasonPollExternalId_SeasonPoll_PollId",
                table: "SeasonPollExternalId");

            migrationBuilder.DropIndex(
                name: "IX_SeasonPollExternalId_PollId",
                table: "SeasonPollExternalId");

            migrationBuilder.DropColumn(
                name: "PollId",
                table: "SeasonPollExternalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PollId",
                table: "SeasonPollExternalId",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_SeasonPollExternalId_PollId",
                table: "SeasonPollExternalId",
                column: "PollId");

            migrationBuilder.AddForeignKey(
                name: "FK_SeasonPollExternalId_SeasonPoll_PollId",
                table: "SeasonPollExternalId",
                column: "PollId",
                principalTable: "SeasonPoll",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
