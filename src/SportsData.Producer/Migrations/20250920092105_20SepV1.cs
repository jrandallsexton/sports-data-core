using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _20SepV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionProbability_Play_PlayId",
                table: "CompetitionProbability");

            migrationBuilder.DropTable(
                name: "PlayExternalId");

            migrationBuilder.DropTable(
                name: "Play");

            migrationBuilder.CreateTable(
                name: "CompetitionPlay",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriveId = table.Column<Guid>(type: "uuid", nullable: true),
                    EspnId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: true),
                    SequenceNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    TypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ShortText = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    AlternativeText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ShortAlternativeText = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    AwayScore = table.Column<int>(type: "integer", nullable: false),
                    HomeScore = table.Column<int>(type: "integer", nullable: false),
                    PeriodNumber = table.Column<int>(type: "integer", nullable: false),
                    ClockValue = table.Column<double>(type: "double precision", nullable: false),
                    ClockDisplayValue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ScoringPlay = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<bool>(type: "boolean", nullable: false),
                    ScoreValue = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TeamFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDown = table.Column<int>(type: "integer", nullable: true),
                    StartDistance = table.Column<int>(type: "integer", nullable: true),
                    StartYardLine = table.Column<int>(type: "integer", nullable: true),
                    StartYardsToEndzone = table.Column<int>(type: "integer", nullable: true),
                    StartTeamFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    EndDown = table.Column<int>(type: "integer", nullable: true),
                    EndDistance = table.Column<int>(type: "integer", nullable: true),
                    EndYardLine = table.Column<int>(type: "integer", nullable: true),
                    EndYardsToEndzone = table.Column<int>(type: "integer", nullable: true),
                    StatYardage = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionPlay", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionPlay_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompetitionPlay_Drive_DriveId",
                        column: x => x.DriveId,
                        principalTable: "Drive",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompetitionPlayExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionPlayId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionPlayExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionPlayExternalId_CompetitionPlay_CompetitionPlayId",
                        column: x => x.CompetitionPlayId,
                        principalTable: "CompetitionPlay",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPlay_CompetitionId",
                table: "CompetitionPlay",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPlay_DriveId",
                table: "CompetitionPlay",
                column: "DriveId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionPlayExternalId_CompetitionPlayId",
                table: "CompetitionPlayExternalId",
                column: "CompetitionPlayId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionProbability_CompetitionPlay_PlayId",
                table: "CompetitionProbability",
                column: "PlayId",
                principalTable: "CompetitionPlay",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionProbability_CompetitionPlay_PlayId",
                table: "CompetitionProbability");

            migrationBuilder.DropTable(
                name: "CompetitionPlayExternalId");

            migrationBuilder.DropTable(
                name: "CompetitionPlay");

            migrationBuilder.CreateTable(
                name: "Play",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriveId = table.Column<Guid>(type: "uuid", nullable: true),
                    AlternativeText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AwayScore = table.Column<int>(type: "integer", nullable: false),
                    ClockDisplayValue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ClockValue = table.Column<double>(type: "double precision", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDistance = table.Column<int>(type: "integer", nullable: true),
                    EndDown = table.Column<int>(type: "integer", nullable: true),
                    EndYardLine = table.Column<int>(type: "integer", nullable: true),
                    EndYardsToEndzone = table.Column<int>(type: "integer", nullable: true),
                    EspnId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    HomeScore = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PeriodNumber = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<bool>(type: "boolean", nullable: false),
                    ScoreValue = table.Column<int>(type: "integer", nullable: false),
                    ScoringPlay = table.Column<bool>(type: "boolean", nullable: false),
                    SequenceNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ShortAlternativeText = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    ShortText = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    StartDistance = table.Column<int>(type: "integer", nullable: true),
                    StartDown = table.Column<int>(type: "integer", nullable: true),
                    StartTeamFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartYardLine = table.Column<int>(type: "integer", nullable: true),
                    StartYardsToEndzone = table.Column<int>(type: "integer", nullable: true),
                    StatYardage = table.Column<int>(type: "integer", nullable: false),
                    TeamFranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    TypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Play", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Play_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Play_Drive_DriveId",
                        column: x => x.DriveId,
                        principalTable: "Drive",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayExternalId",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayExternalId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayExternalId_Play_PlayId",
                        column: x => x.PlayId,
                        principalTable: "Play",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Play_CompetitionId",
                table: "Play",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Play_DriveId",
                table: "Play",
                column: "DriveId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayExternalId_PlayId",
                table: "PlayExternalId",
                column: "PlayId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionProbability_Play_PlayId",
                table: "CompetitionProbability",
                column: "PlayId",
                principalTable: "Play",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
