using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SportsData.Api.Migrations
{
    /// <inheritdoc />
    public partial class PlayerScoringMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerScoringRuleSet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerScoringRuleSet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerScoringRule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Points = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    PerUnits = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerScoringRule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerScoringRule_PlayerScoringRuleSet_RuleSetId",
                        column: x => x.RuleSetId,
                        principalTable: "PlayerScoringRuleSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PlayerScoringRuleSet",
                columns: new[] { "Id", "CreatedBy", "CreatedUtc", "IsDefault", "ModifiedBy", "ModifiedUtc", "Name" },
                values: new object[] { new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), true, null, null, "Standard" });

            migrationBuilder.InsertData(
                table: "PlayerScoringRule",
                columns: new[] { "Id", "CreatedBy", "CreatedUtc", "ModifiedBy", "ModifiedUtc", "PerUnits", "Points", "RuleSetId", "StatKey" },
                values: new object[,]
                {
                    { new Guid("33a100db-526d-5496-8c6e-f478226ab7c6"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, -1m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "derived.fieldGoalsMissed40_49" },
                    { new Guid("559bcedf-28e3-5466-8be3-d8d70ee6c529"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, 6m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "receiving.receivingTouchdowns" },
                    { new Guid("568ce9f4-6213-5254-81c1-d43e53f6e207"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, -2m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "derived.fieldGoalsMissed17_39" },
                    { new Guid("56c9694e-079d-5b02-b955-5566f86e74f2"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 10m, 1m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "rushing.rushingYards" },
                    { new Guid("5ad10eb5-739c-5a5a-afaa-7db306e03a6a"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, -2m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "derived.missedExtraPoints" },
                    { new Guid("629e7224-863f-5ec4-8f4c-4cbd89eb79f1"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, 5m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "derived.fieldGoalsMade50_59" },
                    { new Guid("666e3ffd-3d39-597a-9eb5-364003a9b997"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, 4m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "derived.fieldGoalsMade40_49" },
                    { new Guid("6f215f20-0471-54d3-bee9-a26f1e66d1de"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, 1m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "kicking.extraPointsMade" },
                    { new Guid("7e0e7e54-1fef-5e87-85fb-4485e1aca75c"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, 2m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "passing.twoPtPass" },
                    { new Guid("807b8c25-7215-5f1c-98e8-464686858cd4"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 25m, 1m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "passing.passingYards" },
                    { new Guid("8f721f96-3c86-595f-896b-1e34ec3a0656"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, 6m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "passing.passingTouchdowns" },
                    { new Guid("9ec911b7-d2d0-58cb-b99e-1b112752d462"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, 6m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "derived.fieldGoalsMade60Plus" },
                    { new Guid("b8a73ff8-3494-5459-838f-11e1e07f8720"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, -2m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "fumbles.fumblesLost" },
                    { new Guid("c7d3e6da-6cf1-5d77-bab5-a5d798c5cec2"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, 6m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "rushing.rushingTouchdowns" },
                    { new Guid("c7d7861c-d887-586b-9788-d927009a1cc0"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, 3m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "derived.fieldGoalsMade17_39" },
                    { new Guid("ccc857a3-3b83-52f7-ab39-8c4c6739b3b8"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 10m, 1m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "receiving.receivingYards" },
                    { new Guid("d1c1bbec-7dd3-5a0f-bd7f-0a84597168f4"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, 2m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "receiving.twoPtReception" },
                    { new Guid("e263379b-de5d-5fc8-8179-8914e67f8442"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, -2m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "passing.interceptions" },
                    { new Guid("ea6842c9-d160-5acd-8587-100f54d4ec51"), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 8, 27, 0, 0, 0, 0, DateTimeKind.Utc), null, null, 1m, 2m, new Guid("15c1e173-57ad-5c7e-99a1-c182d4c043ea"), "rushing.twoPtRush" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerScoringRule_RuleSetId_StatKey",
                table: "PlayerScoringRule",
                columns: new[] { "RuleSetId", "StatKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerScoringRule");

            migrationBuilder.DropTable(
                name: "PlayerScoringRuleSet");
        }
    }
}
