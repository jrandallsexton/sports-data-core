using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class _27SepV2_CompTeamOdds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompetitionTeamOdds_CompetitionOddsId",
                table: "CompetitionTeamOdds");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionOddsLink_CompetitionOddsId",
                table: "CompetitionOddsLink");

            migrationBuilder.AlterColumn<decimal>(
                name: "SpreadPriceOpen",
                table: "CompetitionTeamOdds",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SpreadPriceCurrent",
                table: "CompetitionTeamOdds",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SpreadPriceClose",
                table: "CompetitionTeamOdds",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SpreadPointsOpen",
                table: "CompetitionTeamOdds",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SpreadPointsCurrent",
                table: "CompetitionTeamOdds",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SpreadPointsClose",
                table: "CompetitionTeamOdds",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Side",
                table: "CompetitionTeamOdds",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "CompetitionOddsLink",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ShortText",
                table: "CompetitionOddsLink",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Rel",
                table: "CompetitionOddsLink",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "CompetitionOddsLink",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Href",
                table: "CompetitionOddsLink",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionTeamOdds_CompetitionOddsId_Side",
                table: "CompetitionTeamOdds",
                columns: new[] { "CompetitionOddsId", "Side" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionOddsLink_CompetitionOddsId_Rel_Href",
                table: "CompetitionOddsLink",
                columns: new[] { "CompetitionOddsId", "Rel", "Href" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompetitionTeamOdds_CompetitionOddsId_Side",
                table: "CompetitionTeamOdds");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionOddsLink_CompetitionOddsId_Rel_Href",
                table: "CompetitionOddsLink");

            migrationBuilder.AlterColumn<decimal>(
                name: "SpreadPriceOpen",
                table: "CompetitionTeamOdds",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SpreadPriceCurrent",
                table: "CompetitionTeamOdds",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SpreadPriceClose",
                table: "CompetitionTeamOdds",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SpreadPointsOpen",
                table: "CompetitionTeamOdds",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SpreadPointsCurrent",
                table: "CompetitionTeamOdds",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SpreadPointsClose",
                table: "CompetitionTeamOdds",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Side",
                table: "CompetitionTeamOdds",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "CompetitionOddsLink",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ShortText",
                table: "CompetitionOddsLink",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Rel",
                table: "CompetitionOddsLink",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "CompetitionOddsLink",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Href",
                table: "CompetitionOddsLink",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024);

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionTeamOdds_CompetitionOddsId",
                table: "CompetitionTeamOdds",
                column: "CompetitionOddsId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetitionOddsLink_CompetitionOddsId",
                table: "CompetitionOddsLink",
                column: "CompetitionOddsId");
        }
    }
}
