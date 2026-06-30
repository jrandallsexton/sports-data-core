using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class _30JunV1_UserUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows have no username and DisplayName collisions exist, so
            // the unique constraint can't be switched on directly. Add nullable,
            // backfill unique values, then enforce. See
            // docs/username-identity-foundation.md.

            // 1) Nullable column (no default — backfill provides every value).
            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "User",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            // 2) Pin the seed user first so the generic backfill skips it
            //    (matches the HasData value).
            migrationBuilder.UpdateData(
                table: "User",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "Username",
                value: "sportdeets");

            // 3) Backfill remaining rows. Seed = sanitized email local-part
            //    (lowercased, '+tag' and illegal chars stripped); fall back to a
            //    DisplayName slug, then "user". Collisions within a seed get the
            //    shortest numeric suffix via row_number(). The full email is never
            //    used (privacy) — only the local-part.
            migrationBuilder.Sql(@"
                WITH base AS (
                    SELECT ""Id"",
                           CASE
                               WHEN length(es) >= 3 THEN left(es, 30)
                               WHEN length(ds) >= 3 THEN left(ds, 30)
                               ELSE 'user'
                           END AS seed
                    FROM (
                        SELECT ""Id"",
                               regexp_replace(split_part(split_part(lower(""Email""), '@', 1), '+', 1), '[^a-z0-9_]', '', 'g') AS es,
                               regexp_replace(lower(""DisplayName""), '[^a-z0-9_]', '', 'g') AS ds
                        FROM ""User""
                        WHERE ""Username"" IS NULL
                    ) s
                ),
                ranked AS (
                    SELECT ""Id"", seed,
                           row_number() OVER (PARTITION BY seed ORDER BY ""Id"") AS rn
                    FROM base
                )
                UPDATE ""User"" u
                SET ""Username"" = CASE
                        WHEN r.rn = 1 THEN r.seed
                        ELSE left(r.seed, 30 - length(r.rn::text)) || r.rn::text
                    END
                FROM ranked r
                WHERE u.""Id"" = r.""Id"";
            ");

            // 4) Enforce NOT NULL now that every row has a value.
            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "User",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            // 5) Case-insensitive uniqueness (values are stored already-lowercased).
            migrationBuilder.CreateIndex(
                name: "IX_User_Username",
                table: "User",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_Username",
                table: "User");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "User");
        }
    }
}
