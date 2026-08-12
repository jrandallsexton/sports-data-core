using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
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
            //
            // PERMANENT DELETION: the loser records and their stats are
            // gone for good — Down() restores only the old index, never the
            // deleted rows. Acceptable by design: the duplicates are
            // defects, and the survivor rule (latest ModifiedUtc/CreatedUtc)
            // keeps the row the old code would have kept anyway.
            //
            // Lock note: plain CREATE INDEX (not CONCURRENTLY) is
            // deliberate — the table is tiny (~50k rows NCAA, ~4k NFL/MLB;
            // sub-second build), migrations run during deploy restarts when
            // writers are down anyway, and keeping dedup + index in ONE
            // transaction closes the window where a duplicate could re-form
            // between the two steps. CONCURRENTLY cannot run in a
            // transaction and would surrender that atomicity.
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
