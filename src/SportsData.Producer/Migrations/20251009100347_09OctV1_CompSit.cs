using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _09OctV1_CompSit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompetitionSituation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastPlayId = table.Column<Guid>(type: "uuid", nullable: true),
                    Down = table.Column<int>(type: "integer", nullable: false),
                    Distance = table.Column<int>(type: "integer", nullable: false),
                    YardLine = table.Column<int>(type: "integer", nullable: false),
                    IsRedZone = table.Column<bool>(type: "boolean", nullable: false),
                    AwayTimeouts = table.Column<int>(type: "integer", nullable: false),
                    HomeTimeouts = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionSituation", x => x.Id);
                    table.CheckConstraint("CK_CompetitionSituation_AwayTimeouts", "\"AwayTimeouts\" >= 0");
                    table.CheckConstraint("CK_CompetitionSituation_Distance", "\"Distance\" >= 0");
                    table.CheckConstraint("CK_CompetitionSituation_Down", "\"Down\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_CompetitionSituation_HomeTimeouts", "\"HomeTimeouts\" >= 0");
                    table.CheckConstraint("CK_CompetitionSituation_YardLine", "\"YardLine\" BETWEEN -10 AND 110");
                    table.ForeignKey(
                        name: "FK_CompetitionSituation_CompetitionPlay_LastPlayId",
                        column: x => x.LastPlayId,
                        principalTable: "CompetitionPlay",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetitionSituation_Competition_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionSituation_CompetitionId",
                table: "CompetitionSituation",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionSituation_LastPlayId",
                table: "CompetitionSituation",
                column: "LastPlayId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionSituation");
        }
    }
}
