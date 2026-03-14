using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _14MarV1_CompPowerIndexUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete duplicate CompetitionPowerIndex rows before applying unique constraint.
            // Keeps the row with the latest CreatedUtc for each logical key.
            // Uses NULLS LAST so null timestamps are treated as oldest, and "Id" as tiebreaker.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "CompetitionId", "FranchiseSeasonId", "PowerIndexId"
                               ORDER BY "CreatedUtc" DESC NULLS LAST, "Id" DESC
                           ) AS rn
                    FROM "CompetitionPowerIndex"
                )
                DELETE FROM "CompetitionPowerIndex"
                WHERE "Id" IN (SELECT "Id" FROM ranked WHERE rn > 1);
                """);

            migrationBuilder.DropIndex(
                name: "IX_CompetitionPowerIndex_CompetitionId",
                table: "CompetitionPowerIndex");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPowerIndex_CompetitionId_FranchiseSeasonId_Power~",
                table: "CompetitionPowerIndex",
                columns: new[] { "CompetitionId", "FranchiseSeasonId", "PowerIndexId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompetitionPowerIndex_CompetitionId_FranchiseSeasonId_Power~",
                table: "CompetitionPowerIndex");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPowerIndex_CompetitionId",
                table: "CompetitionPowerIndex",
                column: "CompetitionId");
        }
    }
}
