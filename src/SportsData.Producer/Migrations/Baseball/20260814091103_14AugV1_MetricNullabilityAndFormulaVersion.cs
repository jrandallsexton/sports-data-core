using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class _14AugV1_MetricNullabilityAndFormulaVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "PenaltyYardsPerPlay",
                table: "FranchiseSeasonMetric",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "NetPunt",
                table: "FranchiseSeasonMetric",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)",
                oldPrecision: 6,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "FgPctShrunk",
                table: "FranchiseSeasonMetric",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,4)",
                oldPrecision: 5,
                oldScale: 4);

            migrationBuilder.AddColumn<string>(
                name: "FormulaVersion",
                table: "FranchiseSeasonMetric",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PenaltyYardsPerPlay",
                table: "CompetitionMetric",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "NetPunt",
                table: "CompetitionMetric",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)",
                oldPrecision: 6,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "FgPctShrunk",
                table: "CompetitionMetric",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,4)",
                oldPrecision: 5,
                oldScale: 4);

            migrationBuilder.AddColumn<string>(
                name: "FormulaVersion",
                table: "CompetitionMetric",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            // AUDIT M4/H3: these columns were never computable — the stored
            // zeros are fabricated. Retire them now rather than leaving a
            // mix of 0 and NULL for downstream readers until the recompute
            // campaign lands. FgPctShrunk is deliberately excluded: a
            // stored 0 there can be a legitimate 0-for-N result and needs
            // a recompute, not a blanket update.
            migrationBuilder.Sql(
                """
                UPDATE "CompetitionMetric"
                SET "NetPunt" = NULL, "PenaltyYardsPerPlay" = NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "FranchiseSeasonMetric"
                SET "NetPunt" = NULL, "PenaltyYardsPerPlay" = NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback guard: rows written after Up carry NULLs in all
            // three columns; SET NOT NULL would fail. 0 restores the
            // pre-migration convention.
            migrationBuilder.Sql(
                """
                UPDATE "CompetitionMetric"
                SET "NetPunt" = COALESCE("NetPunt", 0),
                    "PenaltyYardsPerPlay" = COALESCE("PenaltyYardsPerPlay", 0),
                    "FgPctShrunk" = COALESCE("FgPctShrunk", 0);
                """);

            migrationBuilder.Sql(
                """
                UPDATE "FranchiseSeasonMetric"
                SET "NetPunt" = COALESCE("NetPunt", 0),
                    "PenaltyYardsPerPlay" = COALESCE("PenaltyYardsPerPlay", 0),
                    "FgPctShrunk" = COALESCE("FgPctShrunk", 0);
                """);

            migrationBuilder.DropColumn(
                name: "FormulaVersion",
                table: "FranchiseSeasonMetric");

            migrationBuilder.DropColumn(
                name: "FormulaVersion",
                table: "CompetitionMetric");

            migrationBuilder.AlterColumn<decimal>(
                name: "PenaltyYardsPerPlay",
                table: "FranchiseSeasonMetric",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "NetPunt",
                table: "FranchiseSeasonMetric",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)",
                oldPrecision: 6,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "FgPctShrunk",
                table: "FranchiseSeasonMetric",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,4)",
                oldPrecision: 5,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PenaltyYardsPerPlay",
                table: "CompetitionMetric",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "NetPunt",
                table: "CompetitionMetric",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,2)",
                oldPrecision: 6,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "FgPctShrunk",
                table: "CompetitionMetric",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,4)",
                oldPrecision: 5,
                oldScale: 4,
                oldNullable: true);
        }
    }
}
