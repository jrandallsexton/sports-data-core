using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class AthleteNflFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CollegeAthleteRef",
                table: "Athlete",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DebutYear",
                table: "Athlete",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DraftDisplayText",
                table: "Athlete",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DraftRound",
                table: "Athlete",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DraftSelection",
                table: "Athlete",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DraftTeamRef",
                table: "Athlete",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DraftYear",
                table: "Athlete",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Jersey",
                table: "Athlete",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AthleteCareerStatistic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteId = table.Column<Guid>(type: "uuid", nullable: false),
                    SplitId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SplitName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SplitAbbreviation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteCareerStatistic", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteCareerStatistic_Athlete_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athlete",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AthleteCareerStatisticCategory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteCareerStatisticId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Summary = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteCareerStatisticCategory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteCareerStatisticCategory_AthleteCareerStatistic_Athle~",
                        column: x => x.AthleteCareerStatisticId,
                        principalTable: "AthleteCareerStatistic",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AthleteCareerStatisticStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteCareerStatisticCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Abbreviation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DisplayValue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PerGameDisplayValue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PerGameValue = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteCareerStatisticStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteCareerStatisticStat_AthleteCareerStatisticCategory_A~",
                        column: x => x.AthleteCareerStatisticCategoryId,
                        principalTable: "AthleteCareerStatisticCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCareerStatistic_AthleteId",
                table: "AthleteCareerStatistic",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCareerStatisticCategory_AthleteCareerStatisticId",
                table: "AthleteCareerStatisticCategory",
                column: "AthleteCareerStatisticId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCareerStatisticStat_AthleteCareerStatisticCategoryId",
                table: "AthleteCareerStatisticStat",
                column: "AthleteCareerStatisticCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AthleteCareerStatisticStat");

            migrationBuilder.DropTable(
                name: "AthleteCareerStatisticCategory");

            migrationBuilder.DropTable(
                name: "AthleteCareerStatistic");

            migrationBuilder.DropColumn(
                name: "CollegeAthleteRef",
                table: "Athlete");

            migrationBuilder.DropColumn(
                name: "DebutYear",
                table: "Athlete");

            migrationBuilder.DropColumn(
                name: "DraftDisplayText",
                table: "Athlete");

            migrationBuilder.DropColumn(
                name: "DraftRound",
                table: "Athlete");

            migrationBuilder.DropColumn(
                name: "DraftSelection",
                table: "Athlete");

            migrationBuilder.DropColumn(
                name: "DraftTeamRef",
                table: "Athlete");

            migrationBuilder.DropColumn(
                name: "DraftYear",
                table: "Athlete");

            migrationBuilder.DropColumn(
                name: "Jersey",
                table: "Athlete");
        }
    }
}
