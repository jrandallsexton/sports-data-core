using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class MatchupPreviewPromptIdNonNullDropPromptVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent safety net for environments where the operator
            // backfill never ran (e.g. local dev DBs with legacy preview
            // rows): map PromptVersion -> Prompt.Name (names are unique).
            // Prod is a no-op — zero null rows, verified pre-merge. Any row
            // that still has no match fails the SET NOT NULL below loudly,
            // by design. Must run BEFORE PromptVersion is dropped.
            migrationBuilder.Sql("""
                UPDATE "MatchupPreview" mp
                SET "PromptId" = p."Id"
                FROM "Prompt" p
                WHERE mp."PromptId" IS NULL
                  AND mp."PromptVersion" = p."Name";
                """);

            migrationBuilder.DropColumn(
                name: "PromptVersion",
                table: "MatchupPreview");

            migrationBuilder.AlterColumn<Guid>(
                name: "PromptId",
                table: "MatchupPreview",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "PromptId",
                table: "MatchupPreview",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "PromptVersion",
                table: "MatchupPreview",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Repopulate the recreated column from the Prompt row so a
            // rollback is not lossy (LEFT guards names > 50 chars, the
            // column's own limit).
            migrationBuilder.Sql("""
                UPDATE "MatchupPreview" mp
                SET "PromptVersion" = LEFT(p."Name", 50)
                FROM "Prompt" p
                WHERE p."Id" = mp."PromptId";
                """);
        }
    }
}
