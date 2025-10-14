using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _14OctV1_Metrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompetitionMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FranchiseSeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Ypp = table.Column<decimal>(type: "numeric", nullable: false),
                    SuccessRate = table.Column<decimal>(type: "numeric", nullable: false),
                    ExplosiveRate = table.Column<decimal>(type: "numeric", nullable: false),
                    PointsPerDrive = table.Column<decimal>(type: "numeric", nullable: false),
                    ThirdFourthRate = table.Column<decimal>(type: "numeric", nullable: false),
                    RzTdRate = table.Column<decimal>(type: "numeric", nullable: true),
                    RzScoreRate = table.Column<decimal>(type: "numeric", nullable: true),
                    OppYpp = table.Column<decimal>(type: "numeric", nullable: false),
                    OppSuccessRate = table.Column<decimal>(type: "numeric", nullable: false),
                    OppExplosiveRate = table.Column<decimal>(type: "numeric", nullable: false),
                    OppPointsPerDrive = table.Column<decimal>(type: "numeric", nullable: false),
                    OppThirdFourthRate = table.Column<decimal>(type: "numeric", nullable: false),
                    OppRzTdRate = table.Column<decimal>(type: "numeric", nullable: true),
                    OppScoreTdRate = table.Column<decimal>(type: "numeric", nullable: true),
                    NetPunt = table.Column<decimal>(type: "numeric", nullable: false),
                    FgPctShrunk = table.Column<decimal>(type: "numeric", nullable: false),
                    FieldPosDiff = table.Column<decimal>(type: "numeric", nullable: false),
                    TurnoverMarginPerDrive = table.Column<decimal>(type: "numeric", nullable: false),
                    PenaltyYardsPerPlay = table.Column<decimal>(type: "numeric", nullable: false),
                    ComputedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InputsHash = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionMetrics", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionMetrics");
        }
    }
}
