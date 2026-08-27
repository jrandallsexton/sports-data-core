using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class PlayerLineupScorePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsScoreFinal",
                table: "PlayerLineupSlot",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Points",
                table: "PlayerLineupSlot",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatLine",
                table: "PlayerLineupSlot",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScoreUpdatedUtc",
                table: "PlayerLineup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPoints",
                table: "PlayerLineup",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsScoreFinal",
                table: "PlayerLineupSlot");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "PlayerLineupSlot");

            migrationBuilder.DropColumn(
                name: "StatLine",
                table: "PlayerLineupSlot");

            migrationBuilder.DropColumn(
                name: "ScoreUpdatedUtc",
                table: "PlayerLineup");

            migrationBuilder.DropColumn(
                name: "TotalPoints",
                table: "PlayerLineup");
        }
    }
}
