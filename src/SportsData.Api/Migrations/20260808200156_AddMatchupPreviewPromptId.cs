using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchupPreviewPromptId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PromptId",
                table: "MatchupPreview",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchupPreview_PromptId",
                table: "MatchupPreview",
                column: "PromptId");

            migrationBuilder.AddForeignKey(
                name: "FK_MatchupPreview_Prompt_PromptId",
                table: "MatchupPreview",
                column: "PromptId",
                principalTable: "Prompt",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchupPreview_Prompt_PromptId",
                table: "MatchupPreview");

            migrationBuilder.DropIndex(
                name: "IX_MatchupPreview_PromptId",
                table: "MatchupPreview");

            migrationBuilder.DropColumn(
                name: "PromptId",
                table: "MatchupPreview");
        }
    }
}
