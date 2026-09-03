using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class ModelConsensusLab : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Gateway",
                table: "Model",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompletionTokens",
                table: "MatchupPreviewPrompt",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LatencyMs",
                table: "MatchupPreviewPrompt",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModelId",
                table: "MatchupPreviewPrompt",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PredictedSpreadWinnerId",
                table: "MatchupPreviewPrompt",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PredictedStraightUpWinnerId",
                table: "MatchupPreviewPrompt",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PromptTokens",
                table: "MatchupPreviewPrompt",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gateway",
                table: "Model");

            migrationBuilder.DropColumn(
                name: "CompletionTokens",
                table: "MatchupPreviewPrompt");

            migrationBuilder.DropColumn(
                name: "LatencyMs",
                table: "MatchupPreviewPrompt");

            migrationBuilder.DropColumn(
                name: "ModelId",
                table: "MatchupPreviewPrompt");

            migrationBuilder.DropColumn(
                name: "PredictedSpreadWinnerId",
                table: "MatchupPreviewPrompt");

            migrationBuilder.DropColumn(
                name: "PredictedStraightUpWinnerId",
                table: "MatchupPreviewPrompt");

            migrationBuilder.DropColumn(
                name: "PromptTokens",
                table: "MatchupPreviewPrompt");
        }
    }
}
