using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class FranchiseSeasonRecordNaturalKeyUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FranchiseSeasonRecord_FranchiseSeasonId",
                table: "FranchiseSeasonRecord");

            // Deduplicate before enforcing uniqueness: the pre-upsert
            // delete/re-add processor raced under at-least-once delivery and
            // left duplicate (FranchiseSeasonId, Name, Type) groups behind
            // (verified in prod: 130 NCAAFB / 27 NFL / 17 MLB groups).
            // Keep the most recently touched row per key; delete the losers'
            // stats first, then the losers.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "FranchiseSeasonId", "Name", "Type"
                        ORDER BY COALESCE("ModifiedUtc", "CreatedUtc") DESC NULLS LAST, "Id"
                    ) AS rn
                    FROM "FranchiseSeasonRecord"
                )
                DELETE FROM "FranchiseSeasonRecordStat" s
                USING ranked r
                WHERE s."FranchiseSeasonRecordId" = r."Id" AND r.rn > 1;
                """);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "FranchiseSeasonId", "Name", "Type"
                        ORDER BY COALESCE("ModifiedUtc", "CreatedUtc") DESC NULLS LAST, "Id"
                    ) AS rn
                    FROM "FranchiseSeasonRecord"
                )
                DELETE FROM "FranchiseSeasonRecord" fr
                USING ranked r
                WHERE fr."Id" = r."Id" AND r.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRecord_FranchiseSeasonId_Name_Type",
                table: "FranchiseSeasonRecord",
                columns: new[] { "FranchiseSeasonId", "Name", "Type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FranchiseSeasonRecord_FranchiseSeasonId_Name_Type",
                table: "FranchiseSeasonRecord");

            migrationBuilder.CreateIndex(
                name: "IX_FranchiseSeasonRecord_FranchiseSeasonId",
                table: "FranchiseSeasonRecord",
                column: "FranchiseSeasonId");
        }
    }
}
