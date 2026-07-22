using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _22JulV2_SeedCompetitionSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "State",
                table: "CompetitionSource",
                type: "character varying(75)",
                maxLength: 75,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "CompetitionSource",
                type: "character varying(75)",
                maxLength: 75,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "CompetitionSource",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            // Idempotent seed: prod databases were already hot-seeded manually to
            // stop the FK-violation storm, so a plain INSERT would fail on duplicate
            // keys. ON CONFLICT keeps this safe on already-seeded and fresh DBs alike.
            migrationBuilder.Sql(@"
                INSERT INTO ""CompetitionSource"" (""Id"", ""Description"", ""State"", ""CreatedUtc"", ""CreatedBy"")
                VALUES
                    (1, 'basic/manual', 'basic', TIMESTAMPTZ '2024-01-01 00:00:00+00', '00000000-0000-0000-0000-000000000000'),
                    (2, 'feed',         'full',  TIMESTAMPTZ '2024-01-01 00:00:00+00', '00000000-0000-0000-0000-000000000000'),
                    (4, 'official',     'full',  TIMESTAMPTZ '2024-01-01 00:00:00+00', '00000000-0000-0000-0000-000000000000')
                ON CONFLICT (""Id"") DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CompetitionSource",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "CompetitionSource",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "CompetitionSource",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.AlterColumn<string>(
                name: "State",
                table: "CompetitionSource",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(75)",
                oldMaxLength: 75);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "CompetitionSource",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(75)",
                oldMaxLength: 75);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "CompetitionSource",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }
    }
}
