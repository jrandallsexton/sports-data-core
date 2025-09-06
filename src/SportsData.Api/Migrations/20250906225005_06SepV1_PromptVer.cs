using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class _06SepV1_PromptVer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatchupPreview_ContestId",
                table: "MatchupPreview");

            migrationBuilder.AddColumn<string>(
                name: "PromptVersion",
                table: "MatchupPreview",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchupPreview_ContestId_Model_PromptVersion",
                table: "MatchupPreview",
                columns: new[] { "ContestId", "Model", "PromptVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatchupPreview_ContestId_Model_PromptVersion",
                table: "MatchupPreview");

            migrationBuilder.DropColumn(
                name: "PromptVersion",
                table: "MatchupPreview");

            migrationBuilder.CreateIndex(
                name: "IX_MatchupPreview_ContestId",
                table: "MatchupPreview",
                column: "ContestId",
                unique: true);
        }
    }
}
