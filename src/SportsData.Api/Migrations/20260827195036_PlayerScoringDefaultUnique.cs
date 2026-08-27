using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class PlayerScoringDefaultUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reconcile any pre-existing duplicate defaults so the filtered
            // unique index can build: keep the OLDEST default (ties broken
            // by Id for determinism), clear the rest. Rule-set edits are
            // operator SQL by design, so a hand-inserted second default is
            // not impossible on an existing database.
            migrationBuilder.Sql("""
                UPDATE "PlayerScoringRuleSet" SET "IsDefault" = false
                WHERE "IsDefault" AND "Id" <> (
                    SELECT "Id" FROM "PlayerScoringRuleSet"
                    WHERE "IsDefault"
                    ORDER BY "CreatedUtc", "Id"
                    LIMIT 1);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerScoringRuleSet_IsDefault",
                table: "PlayerScoringRuleSet",
                column: "IsDefault",
                unique: true,
                filter: "\"IsDefault\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerScoringRuleSet_IsDefault",
                table: "PlayerScoringRuleSet");
        }
    }
}
