using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class _22JulV1_AthleteStatusNameNormalizedUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameNormalized",
                table: "AthleteStatus",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Self-heal: collapse case-insensitive duplicate AthleteStatus rows
            // that predate the unique index (prod has e.g. both "Inactive" and
            // "inactive"). Pick one deterministic winner per lower(Name), repoint
            // the FK references (Athlete.StatusId, AthleteSeason.StatusId) onto the
            // winner, then delete the losers — otherwise the unique index below
            // aborts. No-op on collections without duplicates.
            migrationBuilder.Sql(@"
                UPDATE ""Athlete"" a
                SET ""StatusId"" = m.winner_id
                FROM (
                    SELECT ""Id"" AS loser_id,
                           first_value(""Id"") OVER (PARTITION BY lower(""Name"") ORDER BY ""Id"") AS winner_id
                    FROM ""AthleteStatus""
                    WHERE ""Name"" IS NOT NULL
                ) m
                WHERE a.""StatusId"" = m.loser_id AND m.loser_id <> m.winner_id;");

            migrationBuilder.Sql(@"
                UPDATE ""AthleteSeason"" s
                SET ""StatusId"" = m.winner_id
                FROM (
                    SELECT ""Id"" AS loser_id,
                           first_value(""Id"") OVER (PARTITION BY lower(""Name"") ORDER BY ""Id"") AS winner_id
                    FROM ""AthleteStatus""
                    WHERE ""Name"" IS NOT NULL
                ) m
                WHERE s.""StatusId"" = m.loser_id AND m.loser_id <> m.winner_id;");

            migrationBuilder.Sql(@"
                DELETE FROM ""AthleteStatus""
                WHERE ""Id"" IN (
                    SELECT ""Id"" FROM (
                        SELECT ""Id"",
                               row_number() OVER (PARTITION BY lower(""Name"") ORDER BY ""Id"") AS rn
                        FROM ""AthleteStatus""
                        WHERE ""Name"" IS NOT NULL
                    ) d WHERE d.rn > 1
                );");

            // Backfill remaining rows so they're findable by the new normalized
            // lookup and the unique index has values. lower() matches the app-side
            // ToLowerInvariant for the ASCII status names ESPN ships.
            migrationBuilder.Sql(
                "UPDATE \"AthleteStatus\" SET \"NameNormalized\" = lower(\"Name\") WHERE \"Name\" IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteStatus_NameNormalized",
                table: "AthleteStatus",
                column: "NameNormalized",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AthleteStatus_NameNormalized",
                table: "AthleteStatus");

            migrationBuilder.DropColumn(
                name: "NameNormalized",
                table: "AthleteStatus");
        }
    }
}
