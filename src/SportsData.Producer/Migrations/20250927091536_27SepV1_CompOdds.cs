using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _27SepV1_CompOdds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionTeamOddsSnapshot");

            migrationBuilder.DropTable(
                name: "CompetitionTotalsSnapshot");

            migrationBuilder.RenameColumn(
                name: "HeadlineSpreadOdds",
                table: "CompetitionTeamOdds",
                newName: "SpreadPriceOpen");

            migrationBuilder.RenameColumn(
                name: "HeadlineMoneyLine",
                table: "CompetitionTeamOdds",
                newName: "MoneylineOpen");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedUtc",
                table: "CompetitionTeamOdds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CorrectedUtc",
                table: "CompetitionTeamOdds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MoneylineClose",
                table: "CompetitionTeamOdds",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MoneylineCurrent",
                table: "CompetitionTeamOdds",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SpreadPointsClose",
                table: "CompetitionTeamOdds",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SpreadPointsCurrent",
                table: "CompetitionTeamOdds",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SpreadPointsOpen",
                table: "CompetitionTeamOdds",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SpreadPriceClose",
                table: "CompetitionTeamOdds",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SpreadPriceCurrent",
                table: "CompetitionTeamOdds",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedUtc",
                table: "CompetitionOdds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CorrectedUtc",
                table: "CompetitionOdds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverPriceClose",
                table: "CompetitionOdds",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverPriceCurrent",
                table: "CompetitionOdds",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverPriceOpen",
                table: "CompetitionOdds",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPointsClose",
                table: "CompetitionOdds",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPointsCurrent",
                table: "CompetitionOdds",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPointsOpen",
                table: "CompetitionOdds",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnderPriceClose",
                table: "CompetitionOdds",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnderPriceCurrent",
                table: "CompetitionOdds",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnderPriceOpen",
                table: "CompetitionOdds",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedUtc",
                table: "CompetitionTeamOdds");

            migrationBuilder.DropColumn(
                name: "CorrectedUtc",
                table: "CompetitionTeamOdds");

            migrationBuilder.DropColumn(
                name: "MoneylineClose",
                table: "CompetitionTeamOdds");

            migrationBuilder.DropColumn(
                name: "MoneylineCurrent",
                table: "CompetitionTeamOdds");

            migrationBuilder.DropColumn(
                name: "SpreadPointsClose",
                table: "CompetitionTeamOdds");

            migrationBuilder.DropColumn(
                name: "SpreadPointsCurrent",
                table: "CompetitionTeamOdds");

            migrationBuilder.DropColumn(
                name: "SpreadPointsOpen",
                table: "CompetitionTeamOdds");

            migrationBuilder.DropColumn(
                name: "SpreadPriceClose",
                table: "CompetitionTeamOdds");

            migrationBuilder.DropColumn(
                name: "SpreadPriceCurrent",
                table: "CompetitionTeamOdds");

            migrationBuilder.DropColumn(
                name: "ClosedUtc",
                table: "CompetitionOdds");

            migrationBuilder.DropColumn(
                name: "CorrectedUtc",
                table: "CompetitionOdds");

            migrationBuilder.DropColumn(
                name: "OverPriceClose",
                table: "CompetitionOdds");

            migrationBuilder.DropColumn(
                name: "OverPriceCurrent",
                table: "CompetitionOdds");

            migrationBuilder.DropColumn(
                name: "OverPriceOpen",
                table: "CompetitionOdds");

            migrationBuilder.DropColumn(
                name: "TotalPointsClose",
                table: "CompetitionOdds");

            migrationBuilder.DropColumn(
                name: "TotalPointsCurrent",
                table: "CompetitionOdds");

            migrationBuilder.DropColumn(
                name: "TotalPointsOpen",
                table: "CompetitionOdds");

            migrationBuilder.DropColumn(
                name: "UnderPriceClose",
                table: "CompetitionOdds");

            migrationBuilder.DropColumn(
                name: "UnderPriceCurrent",
                table: "CompetitionOdds");

            migrationBuilder.DropColumn(
                name: "UnderPriceOpen",
                table: "CompetitionOdds");

            migrationBuilder.RenameColumn(
                name: "SpreadPriceOpen",
                table: "CompetitionTeamOdds",
                newName: "HeadlineSpreadOdds");

            migrationBuilder.RenameColumn(
                name: "MoneylineOpen",
                table: "CompetitionTeamOdds",
                newName: "HeadlineMoneyLine");

            migrationBuilder.CreateTable(
                name: "CompetitionTeamOddsSnapshot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionTeamOddsId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FetchedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: true),
                    IsUnderdog = table.Column<bool>(type: "boolean", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MoneylineAlt = table.Column<string>(type: "text", nullable: true),
                    MoneylineAmerican = table.Column<string>(type: "text", nullable: true),
                    MoneylineAmericanNum = table.Column<int>(type: "integer", nullable: true),
                    MoneylineDecimal = table.Column<decimal>(type: "numeric", nullable: true),
                    MoneylineDisplay = table.Column<string>(type: "text", nullable: true),
                    MoneylineFraction = table.Column<string>(type: "text", nullable: true),
                    MoneylineOutcome = table.Column<string>(type: "text", nullable: true),
                    MoneylineValue = table.Column<decimal>(type: "numeric", nullable: true),
                    Phase = table.Column<string>(type: "text", nullable: false),
                    PointSpreadNum = table.Column<decimal>(type: "numeric", nullable: true),
                    PointSpreadRaw = table.Column<string>(type: "text", nullable: true),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: true),
                    SpreadAlt = table.Column<string>(type: "text", nullable: true),
                    SpreadAmerican = table.Column<string>(type: "text", nullable: true),
                    SpreadDecimal = table.Column<decimal>(type: "numeric", nullable: true),
                    SpreadDisplay = table.Column<string>(type: "text", nullable: true),
                    SpreadFraction = table.Column<string>(type: "text", nullable: true),
                    SpreadOutcome = table.Column<string>(type: "text", nullable: true),
                    SpreadValue = table.Column<decimal>(type: "numeric", nullable: true),
                    TeamOddsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionTeamOddsSnapshot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionTeamOddsSnapshot_CompetitionTeamOdds_Competition~",
                        column: x => x.CompetitionTeamOddsId,
                        principalTable: "CompetitionTeamOdds",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CompetitionTotalsSnapshot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionOddsId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FetchedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OverAlt = table.Column<string>(type: "text", nullable: true),
                    OverAmerican = table.Column<string>(type: "text", nullable: true),
                    OverDecimal = table.Column<decimal>(type: "numeric", nullable: true),
                    OverDisplay = table.Column<string>(type: "text", nullable: true),
                    OverFraction = table.Column<string>(type: "text", nullable: true),
                    OverOutcome = table.Column<string>(type: "text", nullable: true),
                    OverValue = table.Column<decimal>(type: "numeric", nullable: true),
                    Phase = table.Column<string>(type: "text", nullable: false),
                    SourceUrlHash = table.Column<string>(type: "text", nullable: true),
                    TotalAlt = table.Column<string>(type: "text", nullable: true),
                    TotalAmerican = table.Column<string>(type: "text", nullable: true),
                    TotalDecimal = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalDisplay = table.Column<string>(type: "text", nullable: true),
                    TotalFraction = table.Column<string>(type: "text", nullable: true),
                    TotalValue = table.Column<decimal>(type: "numeric", nullable: true),
                    UnderAlt = table.Column<string>(type: "text", nullable: true),
                    UnderAmerican = table.Column<string>(type: "text", nullable: true),
                    UnderDecimal = table.Column<decimal>(type: "numeric", nullable: true),
                    UnderDisplay = table.Column<string>(type: "text", nullable: true),
                    UnderFraction = table.Column<string>(type: "text", nullable: true),
                    UnderOutcome = table.Column<string>(type: "text", nullable: true),
                    UnderValue = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionTotalsSnapshot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionTotalsSnapshot_CompetitionOdds_CompetitionOddsId",
                        column: x => x.CompetitionOddsId,
                        principalTable: "CompetitionOdds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionTeamOddsSnapshot_CompetitionTeamOddsId",
                table: "CompetitionTeamOddsSnapshot",
                column: "CompetitionTeamOddsId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionTotalsSnapshot_CompetitionOddsId",
                table: "CompetitionTotalsSnapshot",
                column: "CompetitionOddsId");
        }
    }
}
