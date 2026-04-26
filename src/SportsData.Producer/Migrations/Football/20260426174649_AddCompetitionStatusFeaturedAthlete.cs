using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class AddCompetitionStatusFeaturedAthlete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "CompetitionStatusFeaturedAthlete",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionStatusId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CompetitionStatusFeaturedAthlete", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionStatusFeaturedAthlete_CompetitionStatus_Competit~",
                        column: x => x.CompetitionStatusId,
                        principalTable: "CompetitionStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionStatusFeaturedAthlete_CompetitionStatusId",
                table: "CompetitionStatusFeaturedAthlete",
                column: "CompetitionStatusId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionStatusFeaturedAthlete");

            migrationBuilder.DropColumn(
                name: "HalfInning",
                table: "CompetitionStatus");

            migrationBuilder.DropColumn(
                name: "PeriodPrefix",
                table: "CompetitionStatus");
        }
    }
}
