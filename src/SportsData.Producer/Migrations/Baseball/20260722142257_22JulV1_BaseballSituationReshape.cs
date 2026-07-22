using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class _22JulV1_BaseballSituationReshape : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing baseball situation rows were football-shaped placeholders
            // (Down/Distance/YardLine = 0, no real baseball data). They have no
            // inbound references (the notes table is created below), so clear them
            // before the reshape — they'll be re-sourced on replay. This also
            // avoids leaving rows with an empty TPH discriminator.
            migrationBuilder.Sql("DELETE FROM \"CompetitionSituation\";");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CompetitionSituation_AwayTimeouts",
                table: "CompetitionSituation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CompetitionSituation_Distance",
                table: "CompetitionSituation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CompetitionSituation_Down",
                table: "CompetitionSituation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CompetitionSituation_HomeTimeouts",
                table: "CompetitionSituation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CompetitionSituation_YardLine",
                table: "CompetitionSituation");

            migrationBuilder.DropColumn(
                name: "AwayTimeouts",
                table: "CompetitionSituation");

            migrationBuilder.DropColumn(
                name: "Distance",
                table: "CompetitionSituation");

            migrationBuilder.DropColumn(
                name: "Down",
                table: "CompetitionSituation");

            migrationBuilder.DropColumn(
                name: "HomeTimeouts",
                table: "CompetitionSituation");

            migrationBuilder.DropColumn(
                name: "IsRedZone",
                table: "CompetitionSituation");

            migrationBuilder.DropColumn(
                name: "YardLine",
                table: "CompetitionSituation");

            migrationBuilder.AddColumn<int>(
                name: "Balls",
                table: "CompetitionSituation",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "CompetitionSituation",
                type: "character varying(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "OnFirstAthleteSeasonId",
                table: "CompetitionSituation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OnSecondAthleteSeasonId",
                table: "CompetitionSituation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OnThirdAthleteSeasonId",
                table: "CompetitionSituation",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Outs",
                table: "CompetitionSituation",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Strikes",
                table: "CompetitionSituation",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BaseballCompetitionSituationNote",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SituationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Text = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseballCompetitionSituationNote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BaseballCompetitionSituationNote_CompetitionSituation_Situa~",
                        column: x => x.SituationId,
                        principalTable: "CompetitionSituation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionSituation_OnFirstAthleteSeasonId",
                table: "CompetitionSituation",
                column: "OnFirstAthleteSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionSituation_OnSecondAthleteSeasonId",
                table: "CompetitionSituation",
                column: "OnSecondAthleteSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionSituation_OnThirdAthleteSeasonId",
                table: "CompetitionSituation",
                column: "OnThirdAthleteSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseballCompetitionSituationNote_SituationId",
                table: "BaseballCompetitionSituationNote",
                column: "SituationId");

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionSituation_AthleteSeason_OnFirstAthleteSeasonId",
                table: "CompetitionSituation",
                column: "OnFirstAthleteSeasonId",
                principalTable: "AthleteSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionSituation_AthleteSeason_OnSecondAthleteSeasonId",
                table: "CompetitionSituation",
                column: "OnSecondAthleteSeasonId",
                principalTable: "AthleteSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompetitionSituation_AthleteSeason_OnThirdAthleteSeasonId",
                table: "CompetitionSituation",
                column: "OnThirdAthleteSeasonId",
                principalTable: "AthleteSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionSituation_AthleteSeason_OnFirstAthleteSeasonId",
                table: "CompetitionSituation");

            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionSituation_AthleteSeason_OnSecondAthleteSeasonId",
                table: "CompetitionSituation");

            migrationBuilder.DropForeignKey(
                name: "FK_CompetitionSituation_AthleteSeason_OnThirdAthleteSeasonId",
                table: "CompetitionSituation");

            migrationBuilder.DropTable(
                name: "BaseballCompetitionSituationNote");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionSituation_OnFirstAthleteSeasonId",
                table: "CompetitionSituation");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionSituation_OnSecondAthleteSeasonId",
                table: "CompetitionSituation");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionSituation_OnThirdAthleteSeasonId",
                table: "CompetitionSituation");

            migrationBuilder.DropColumn(
                name: "Balls",
                table: "CompetitionSituation");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "CompetitionSituation");

            migrationBuilder.DropColumn(
                name: "OnFirstAthleteSeasonId",
                table: "CompetitionSituation");

            migrationBuilder.DropColumn(
                name: "OnSecondAthleteSeasonId",
                table: "CompetitionSituation");

            migrationBuilder.DropColumn(
                name: "OnThirdAthleteSeasonId",
                table: "CompetitionSituation");

            migrationBuilder.DropColumn(
                name: "Outs",
                table: "CompetitionSituation");

            migrationBuilder.DropColumn(
                name: "Strikes",
                table: "CompetitionSituation");

            migrationBuilder.AddColumn<int>(
                name: "AwayTimeouts",
                table: "CompetitionSituation",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Distance",
                table: "CompetitionSituation",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Down",
                table: "CompetitionSituation",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HomeTimeouts",
                table: "CompetitionSituation",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsRedZone",
                table: "CompetitionSituation",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "YardLine",
                table: "CompetitionSituation",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CompetitionSituation_AwayTimeouts",
                table: "CompetitionSituation",
                sql: "\"AwayTimeouts\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CompetitionSituation_Distance",
                table: "CompetitionSituation",
                sql: "\"Distance\" >= -110");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CompetitionSituation_Down",
                table: "CompetitionSituation",
                sql: "\"Down\" BETWEEN -1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CompetitionSituation_HomeTimeouts",
                table: "CompetitionSituation",
                sql: "\"HomeTimeouts\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CompetitionSituation_YardLine",
                table: "CompetitionSituation",
                sql: "\"YardLine\" BETWEEN 0 AND 100");
        }
    }
}
