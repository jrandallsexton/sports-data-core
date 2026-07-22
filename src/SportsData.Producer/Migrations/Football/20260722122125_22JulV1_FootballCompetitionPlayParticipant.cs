using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _22JulV1_FootballCompetitionPlayParticipant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompetitionPlayParticipant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionPlayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AthleteSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    StatisticsRef = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Discriminator = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionPlayParticipant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionPlayParticipant_CompetitionPlay_CompetitionPlayId",
                        column: x => x.CompetitionPlayId,
                        principalTable: "CompetitionPlay",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPlayParticipant_AthleteSeasonId",
                table: "CompetitionPlayParticipant",
                column: "AthleteSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPlayParticipant_CompetitionPlayId_Type",
                table: "CompetitionPlayParticipant",
                columns: new[] { "CompetitionPlayId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPlayParticipant_PositionId",
                table: "CompetitionPlayParticipant",
                column: "PositionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionPlayParticipant");
        }
    }
}
