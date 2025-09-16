using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _15SepV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AthleteCompetitionStatistic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteCompetitionStatistic", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteCompetitionStatistic_AthleteSeason_AthleteSeasonId",
                        column: x => x.AthleteSeasonId,
                        principalTable: "AthleteSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AthleteCompetitionStatistic_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionCompetitorStatistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionCompetitorStatistics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorStatistics_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorStatistics_FranchiseSeason_FranchiseSe~",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AthleteCompetitionStatisticCategory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteCompetitionStatisticId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    AthleteCompetitionStatisticId1 = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteCompetitionStatisticCategory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteCompetitionStatisticCategory_AthleteCompetitionStati~",
                        column: x => x.AthleteCompetitionStatisticId,
                        principalTable: "AthleteCompetitionStatistic",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AthleteCompetitionStatisticCategory_AthleteCompetitionStat~1",
                        column: x => x.AthleteCompetitionStatisticId1,
                        principalTable: "AthleteCompetitionStatistic",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CompetitionCompetitorStatisticCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionCompetitorStatisticId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Abbreviation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionCompetitorStatisticCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorStatisticCategories_CompetitionCompeti~",
                        column: x => x.CompetitionCompetitorStatisticId,
                        principalTable: "CompetitionCompetitorStatistics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AthleteCompetitionStatisticStat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteCompetitionStatisticCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Abbreviation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    DisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteCompetitionStatisticStat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteCompetitionStatisticStat_AthleteCompetitionStatistic~",
                        column: x => x.AthleteCompetitionStatisticCategoryId,
                        principalTable: "AthleteCompetitionStatisticCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionCompetitorStatisticStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionCompetitorStatisticCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ShortDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Abbreviation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    DisplayValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionCompetitorStatisticStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionCompetitorStatisticStats_CompetitionCompetitorSt~",
                        column: x => x.CompetitionCompetitorStatisticCategoryId,
                        principalTable: "CompetitionCompetitorStatisticCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetitionStatistic_AthleteSeasonId_CompetitionId",
                table: "AthleteCompetitionStatistic",
                columns: new[] { "AthleteSeasonId", "CompetitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetitionStatistic_CompetitionId",
                table: "AthleteCompetitionStatistic",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetitionStatisticCategory_AthleteCompetitionStat~1",
                table: "AthleteCompetitionStatisticCategory",
                column: "AthleteCompetitionStatisticId1");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetitionStatisticCategory_AthleteCompetitionStati~",
                table: "AthleteCompetitionStatisticCategory",
                column: "AthleteCompetitionStatisticId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetitionStatisticStat_AthleteCompetitionStatistic~",
                table: "AthleteCompetitionStatisticStat",
                column: "AthleteCompetitionStatisticCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorStatisticCategories_CompetitionCompeti~",
                table: "CompetitionCompetitorStatisticCategories",
                column: "CompetitionCompetitorStatisticId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorStatistics_CompetitionId",
                table: "CompetitionCompetitorStatistics",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorStatistics_FranchiseSeasonId_Competiti~",
                table: "CompetitionCompetitorStatistics",
                columns: new[] { "FranchiseSeasonId", "CompetitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionCompetitorStatisticStats_CompetitionCompetitorSt~",
                table: "CompetitionCompetitorStatisticStats",
                column: "CompetitionCompetitorStatisticCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AthleteCompetitionStatisticStat");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorStatisticStats");

            migrationBuilder.DropTable(
                name: "AthleteCompetitionStatisticCategory");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorStatisticCategories");

            migrationBuilder.DropTable(
                name: "AthleteCompetitionStatistic");

            migrationBuilder.DropTable(
                name: "CompetitionCompetitorStatistics");
        }
    }
}
