using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PromptId",
                table: "MatchupPreviewPrompt",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Prompt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: true),
                    WithStats = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompt", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Prompt_Name",
                table: "Prompt",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prompt_Type_IsDefault",
                table: "Prompt",
                columns: new[] { "Type", "IsDefault" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Prompt");

            migrationBuilder.DropColumn(
                name: "PromptId",
                table: "MatchupPreviewPrompt");
        }
    }
}
