using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _22JulV1_SituationTphSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "YardLine",
                table: "CompetitionSituation",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<bool>(
                name: "IsRedZone",
                table: "CompetitionSituation",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<int>(
                name: "HomeTimeouts",
                table: "CompetitionSituation",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Down",
                table: "CompetitionSituation",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "Distance",
                table: "CompetitionSituation",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "AwayTimeouts",
                table: "CompetitionSituation",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "CompetitionSituation",
                type: "character varying(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "");

            // Existing situation rows in the football DB are all football
            // situations — tag them so EF's TPH discriminator can materialize them
            // (the AddColumn default of "" is not a valid discriminator value).
            migrationBuilder.Sql(
                "UPDATE \"CompetitionSituation\" SET \"Discriminator\" = 'FootballCompetitionSituation';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "CompetitionSituation");

            // AlterColumn's defaultValue only sets the DEFAULT for future inserts;
            // it does NOT backfill existing rows. Coalesce any NULLs to the column
            // defaults first so the SET NOT NULL below can't fail on a stray NULL.
            migrationBuilder.Sql(@"
                UPDATE ""CompetitionSituation"" SET
                    ""YardLine"" = COALESCE(""YardLine"", 0),
                    ""Down"" = COALESCE(""Down"", 0),
                    ""Distance"" = COALESCE(""Distance"", 0),
                    ""HomeTimeouts"" = COALESCE(""HomeTimeouts"", 0),
                    ""AwayTimeouts"" = COALESCE(""AwayTimeouts"", 0),
                    ""IsRedZone"" = COALESCE(""IsRedZone"", false)
                WHERE ""YardLine"" IS NULL OR ""Down"" IS NULL OR ""Distance"" IS NULL
                   OR ""HomeTimeouts"" IS NULL OR ""AwayTimeouts"" IS NULL OR ""IsRedZone"" IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "YardLine",
                table: "CompetitionSituation",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsRedZone",
                table: "CompetitionSituation",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "HomeTimeouts",
                table: "CompetitionSituation",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Down",
                table: "CompetitionSituation",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Distance",
                table: "CompetitionSituation",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AwayTimeouts",
                table: "CompetitionSituation",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
