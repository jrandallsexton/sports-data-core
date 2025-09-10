using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class _10SepV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatchupPreview_ContestId_Model_PromptVersion",
                table: "MatchupPreview");

            migrationBuilder.AddColumn<int>(
                name: "IterationsRequired",
                table: "MatchupPreview",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IterationsRequired",
                table: "MatchupPreview");

            migrationBuilder.CreateIndex(
                name: "IX_MatchupPreview_ContestId_Model_PromptVersion",
                table: "MatchupPreview",
                columns: new[] { "ContestId", "Model", "PromptVersion" },
                unique: true);
        }
    }
}
