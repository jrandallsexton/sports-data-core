using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _25SepV1_CompOdds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FetchedUtc",
                table: "CompetitionTotalsSnapshot",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "SourceUrlHash",
                table: "CompetitionTotalsSnapshot",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FetchedUtc",
                table: "CompetitionTeamOddsSnapshot",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "CompetitionTeamOddsSnapshot",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUnderdog",
                table: "CompetitionTeamOddsSnapshot",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MoneylineAmericanNum",
                table: "CompetitionTeamOddsSnapshot",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrlHash",
                table: "CompetitionTeamOddsSnapshot",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropBetsRef",
                table: "CompetitionOdds",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompetitionOddsLink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionOddsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rel = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: true),
                    Href = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: true),
                    ShortText = table.Column<string>(type: "text", nullable: true),
                    IsExternal = table.Column<bool>(type: "boolean", nullable: false),
                    IsPremium = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetitionOddsLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetitionOddsLink_CompetitionOdds_CompetitionOddsId",
                        column: x => x.CompetitionOddsId,
                        principalTable: "CompetitionOdds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionOdds_CompetitionId",
                table: "CompetitionOdds",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionOddsLink_CompetitionOddsId",
                table: "CompetitionOddsLink",
                column: "CompetitionOddsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetitionOddsLink");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionOdds_CompetitionId",
                table: "CompetitionOdds");

            migrationBuilder.DropColumn(
                name: "FetchedUtc",
                table: "CompetitionTotalsSnapshot");

            migrationBuilder.DropColumn(
                name: "SourceUrlHash",
                table: "CompetitionTotalsSnapshot");

            migrationBuilder.DropColumn(
                name: "FetchedUtc",
                table: "CompetitionTeamOddsSnapshot");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "CompetitionTeamOddsSnapshot");

            migrationBuilder.DropColumn(
                name: "IsUnderdog",
                table: "CompetitionTeamOddsSnapshot");

            migrationBuilder.DropColumn(
                name: "MoneylineAmericanNum",
                table: "CompetitionTeamOddsSnapshot");

            migrationBuilder.DropColumn(
                name: "SourceUrlHash",
                table: "CompetitionTeamOddsSnapshot");

            migrationBuilder.DropColumn(
                name: "PropBetsRef",
                table: "CompetitionOdds");
        }
    }
}
