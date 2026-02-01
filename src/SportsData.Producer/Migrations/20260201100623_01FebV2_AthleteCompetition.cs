using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _01FebV2_AthleteCompetition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AthleteCompetition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AthleteSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    JerseyNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DidNotPlay = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteCompetition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteCompetition_AthletePosition_PositionId",
                        column: x => x.PositionId,
                        principalTable: "AthletePosition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AthleteCompetition_AthleteSeason_AthleteSeasonId",
                        column: x => x.AthleteSeasonId,
                        principalTable: "AthleteSeason",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AthleteCompetition_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetition_AthleteSeasonId",
                table: "AthleteCompetition",
                column: "AthleteSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetition_CompetitionId_AthleteSeasonId",
                table: "AthleteCompetition",
                columns: new[] { "CompetitionId", "AthleteSeasonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AthleteCompetition_PositionId",
                table: "AthleteCompetition",
                column: "PositionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AthleteCompetition");
        }
    }
}
