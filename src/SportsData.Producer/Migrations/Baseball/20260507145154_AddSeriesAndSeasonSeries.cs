using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class AddSeriesAndSeasonSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentSeriesId",
                table: "Competition",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SeasonSeriesId",
                table: "Competition",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SeasonSeries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonYear = table.Column<int>(type: "integer", nullable: false),
                    FranchiseSeasonALowId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonBHighId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Summary = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    TotalCompetitions = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonSeries_FranchiseSeason_FranchiseSeasonALowId",
                        column: x => x.FranchiseSeasonALowId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeasonSeries_FranchiseSeason_FranchiseSeasonBHighId",
                        column: x => x.FranchiseSeasonBHighId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Series",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EspnSeriesId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Summary = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    TotalCompetitions = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeasonSeriesCompetitor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonSeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: false),
                    Ties = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeasonSeriesCompetitor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeasonSeriesCompetitor_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeasonSeriesCompetitor_SeasonSeries_SeasonSeriesId",
                        column: x => x.SeasonSeriesId,
                        principalTable: "SeasonSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeriesCompetitor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: false),
                    Ties = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesCompetitor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeriesCompetitor_FranchiseSeason_FranchiseSeasonId",
                        column: x => x.FranchiseSeasonId,
                        principalTable: "FranchiseSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeriesCompetitor_Series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Competition_CurrentSeriesId",
                table: "Competition",
                column: "CurrentSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_Competition_SeasonSeriesId",
                table: "Competition",
                column: "SeasonSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonSeries_FranchiseSeasonALowId",
                table: "SeasonSeries",
                column: "FranchiseSeasonALowId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonSeries_FranchiseSeasonBHighId",
                table: "SeasonSeries",
                column: "FranchiseSeasonBHighId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonSeries_SeasonYear_FranchiseSeasonALowId_FranchiseSeas~",
                table: "SeasonSeries",
                columns: new[] { "SeasonYear", "FranchiseSeasonALowId", "FranchiseSeasonBHighId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonSeriesCompetitor_FranchiseSeasonId",
                table: "SeasonSeriesCompetitor",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonSeriesCompetitor_SeasonSeriesId_FranchiseSeasonId",
                table: "SeasonSeriesCompetitor",
                columns: new[] { "SeasonSeriesId", "FranchiseSeasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Series_EspnSeriesId",
                table: "Series",
                column: "EspnSeriesId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeriesCompetitor_FranchiseSeasonId",
                table: "SeriesCompetitor",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesCompetitor_SeriesId_FranchiseSeasonId",
                table: "SeriesCompetitor",
                columns: new[] { "SeriesId", "FranchiseSeasonId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Competition_SeasonSeries_SeasonSeriesId",
                table: "Competition",
                column: "SeasonSeriesId",
                principalTable: "SeasonSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Competition_Series_CurrentSeriesId",
                table: "Competition",
                column: "CurrentSeriesId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Competition_SeasonSeries_SeasonSeriesId",
                table: "Competition");

            migrationBuilder.DropForeignKey(
                name: "FK_Competition_Series_CurrentSeriesId",
                table: "Competition");

            migrationBuilder.DropTable(
                name: "SeasonSeriesCompetitor");

            migrationBuilder.DropTable(
                name: "SeriesCompetitor");

            migrationBuilder.DropTable(
                name: "SeasonSeries");

            migrationBuilder.DropTable(
                name: "Series");

            migrationBuilder.DropIndex(
                name: "IX_Competition_CurrentSeriesId",
                table: "Competition");

            migrationBuilder.DropIndex(
                name: "IX_Competition_SeasonSeriesId",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "CurrentSeriesId",
                table: "Competition");

            migrationBuilder.DropColumn(
                name: "SeasonSeriesId",
                table: "Competition");
        }
    }
}
