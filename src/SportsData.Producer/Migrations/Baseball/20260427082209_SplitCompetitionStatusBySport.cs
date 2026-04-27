using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class SplitCompetitionStatusBySport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default to the concrete baseball subtype so the column
            // never carries an empty / unmapped TPH discriminator —
            // existing rows in this DB were all written by the
            // pre-split processor, which is now BaseballCompetitionStatus.
            // The UPDATE backfill below is retained as a defensive
            // idempotent step in case any row somehow predates the
            // default being applied.
            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "CompetitionStatus",
                type: "character varying(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "BaseballCompetitionStatus");

            migrationBuilder.AddColumn<int>(
                name: "HalfInning",
                table: "CompetitionStatus",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PeriodPrefix",
                table: "CompetitionStatus",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            // Existing rows in the baseball DB were inserted before the
            // sport-specific subclass split — backfill the discriminator
            // so EF's typed queries (Set<BaseballCompetitionStatus>)
            // match them. HalfInning / PeriodPrefix stay null on these
            // rows (they were never captured by the previous processor).
            migrationBuilder.Sql(
                "UPDATE \"CompetitionStatus\" SET \"Discriminator\" = 'BaseballCompetitionStatus' WHERE \"Discriminator\" = ''");

            migrationBuilder.CreateTable(
                name: "BaseballCompetitionStatusFeaturedAthlete",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionStatusId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ShortDisplayName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Abbreviation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AthleteRef = table.Column<string>(type: "text", nullable: true),
                    TeamRef = table.Column<string>(type: "text", nullable: true),
                    StatisticsRef = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseballCompetitionStatusFeaturedAthlete", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BaseballCompetitionStatusFeaturedAthlete_CompetitionStatus_~",
                        column: x => x.CompetitionStatusId,
                        principalTable: "CompetitionStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BaseballCompetitionStatusFeaturedAthlete_CompetitionStatusId",
                table: "BaseballCompetitionStatusFeaturedAthlete",
                column: "CompetitionStatusId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BaseballCompetitionStatusFeaturedAthlete");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "CompetitionStatus");

            migrationBuilder.DropColumn(
                name: "HalfInning",
                table: "CompetitionStatus");

            migrationBuilder.DropColumn(
                name: "PeriodPrefix",
                table: "CompetitionStatus");
        }
    }
}
