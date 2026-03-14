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
            migrationBuilder.Sql("""
                DELETE FROM "CompetitionPowerIndex"
                WHERE "Id" NOT IN (
                    SELECT DISTINCT ON ("CompetitionId", "FranchiseSeasonId", "PowerIndexId") "Id"
                    FROM "CompetitionPowerIndex"
                    ORDER BY "CompetitionId", "FranchiseSeasonId", "PowerIndexId", "CreatedUtc" DESC
                );
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
