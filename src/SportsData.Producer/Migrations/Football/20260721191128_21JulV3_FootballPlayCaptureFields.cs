using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _21JulV3_FootballPlayCaptureFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PointAfterAttemptAbbreviation",
                table: "CompetitionPlay",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointAfterAttemptId",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PointAfterAttemptText",
                table: "CompetitionPlay",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointAfterAttemptValue",
                table: "CompetitionPlay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScoringTypeAbbreviation",
                table: "CompetitionPlay",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScoringTypeDisplayName",
                table: "CompetitionPlay",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScoringTypeName",
                table: "CompetitionPlay",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Wallclock",
                table: "CompetitionPlay",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointAfterAttemptAbbreviation",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "PointAfterAttemptId",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "PointAfterAttemptText",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "PointAfterAttemptValue",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "ScoringTypeAbbreviation",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "ScoringTypeDisplayName",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "ScoringTypeName",
                table: "CompetitionPlay");

            migrationBuilder.DropColumn(
                name: "Wallclock",
                table: "CompetitionPlay");
        }
    }
}
