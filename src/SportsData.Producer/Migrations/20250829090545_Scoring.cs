using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class Scoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwayScore",
                table: "Contest",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizedUtc",
                table: "Contest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeScore",
                table: "Contest",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SpreadWinnerFranchiseId",
                table: "Contest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WinnerFranchiseId",
                table: "Contest",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Points",
                table: "CompetitionCompetitor",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwayScore",
                table: "Contest");

            migrationBuilder.DropColumn(
                name: "FinalizedUtc",
                table: "Contest");

            migrationBuilder.DropColumn(
                name: "HomeScore",
                table: "Contest");

            migrationBuilder.DropColumn(
                name: "SpreadWinnerFranchiseId",
                table: "Contest");

            migrationBuilder.DropColumn(
                name: "WinnerFranchiseId",
                table: "Contest");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "CompetitionCompetitor");
        }
    }
}
