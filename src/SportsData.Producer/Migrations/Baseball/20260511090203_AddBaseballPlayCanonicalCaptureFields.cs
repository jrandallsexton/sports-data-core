using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class AddBaseballPlayCanonicalCaptureFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AtBatAthleteSeasonId",
                table: "CompetitionPlay",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HalfInning",
                table: "CompetitionPlay",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            // Non-nullable on the entity (default true). Set defaultValue so
            // existing rows (Football plays + previously-ingested Baseball plays)
            // get a sensible value rather than NULL on TPH read.
            migrationBuilder.AddColumn<bool>(
                name: "IsValid",
                table: "CompetitionPlay",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Outs",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PitchesAbbreviation",
                table: "CompetitionPlay",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PitchesType",
                table: "CompetitionPlay",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PitchingAthleteSeasonId",
                table: "CompetitionPlay",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Wallclock",
                table: "CompetitionPlay",
                type: "timestamp with time zone",
                nullable: true);

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

            migrationBuilder.DropColumn(
                name: "AtBatAthleteSeasonId",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "HalfInning",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "IsValid",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "Outs",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "PitchesAbbreviation",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "PitchesType",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "PitchingAthleteSeasonId",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "Wallclock",
                table: "CompetitionPlay");
        }
    }
}
