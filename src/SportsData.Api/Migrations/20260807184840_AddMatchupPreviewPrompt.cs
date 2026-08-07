using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchupPreviewPrompt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchupPreviewPrompt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    MatchupPreviewId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromptVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    EditorNote = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CharCount = table.Column<int>(type: "integer", nullable: false),
                    EstTokens = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RawResponse = table.Column<string>(type: "text", nullable: true),
                    ResponseValidationErrors = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchupPreviewPrompt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchupPreviewPrompt_MatchupPreview_MatchupPreviewId",
                        column: x => x.MatchupPreviewId,
                        principalTable: "MatchupPreview",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchupPreviewPrompt_ContestId",
                table: "MatchupPreviewPrompt",
                column: "ContestId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchupPreviewPrompt_MatchupPreviewId",
                table: "MatchupPreviewPrompt",
                column: "MatchupPreviewId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchupPreviewPrompt");
        }
    }
}
