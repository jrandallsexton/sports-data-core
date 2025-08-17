using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations
{
    /// <inheritdoc />
    public partial class Aug17V2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeasonRankingEntry_SeasonRankingId_Current",
                table: "SeasonRankingEntry");

            migrationBuilder.DropIndex(
                name: "IX_SeasonRankingEntry_SeasonRankingId_TeamRefUrlHash",
                table: "SeasonRankingEntry");

            migrationBuilder.DropColumn(
                name: "FranchiseId",
                table: "SeasonRankingEntry");

            migrationBuilder.DropColumn(
                name: "TeamRefUrlHash",
                table: "SeasonRankingEntry");

            migrationBuilder.AlterColumn<decimal>(
                name: "Points",
                table: "SeasonRankingEntry",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)");

            migrationBuilder.AlterColumn<Guid>(
                name: "FranchiseSeasonId",
                table: "SeasonRankingEntry",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceList",
                table: "SeasonRankingEntry",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonRankingEntry_FranchiseSeasonId",
                table: "SeasonRankingEntry",
                column: "FranchiseSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonRankingEntry_SeasonRankingId_FranchiseSeasonId_Source~",
                table: "SeasonRankingEntry",
                columns: new[] { "SeasonRankingId", "FranchiseSeasonId", "SourceList" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SeasonRankingEntry_FranchiseSeason_FranchiseSeasonId",
                table: "SeasonRankingEntry",
                column: "FranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SeasonRankingEntry_FranchiseSeason_FranchiseSeasonId",
                table: "SeasonRankingEntry");

            migrationBuilder.DropIndex(
                name: "IX_SeasonRankingEntry_FranchiseSeasonId",
                table: "SeasonRankingEntry");

            migrationBuilder.DropIndex(
                name: "IX_SeasonRankingEntry_SeasonRankingId_FranchiseSeasonId_Source~",
                table: "SeasonRankingEntry");

            migrationBuilder.DropColumn(
                name: "SourceList",
                table: "SeasonRankingEntry");

            migrationBuilder.AlterColumn<decimal>(
                name: "Points",
                table: "SeasonRankingEntry",
                type: "numeric(10,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<Guid>(
                name: "FranchiseSeasonId",
                table: "SeasonRankingEntry",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "FranchiseId",
                table: "SeasonRankingEntry",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamRefUrlHash",
                table: "SeasonRankingEntry",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SeasonRankingEntry_SeasonRankingId_Current",
                table: "SeasonRankingEntry",
                columns: new[] { "SeasonRankingId", "Current" });

            migrationBuilder.CreateIndex(
                name: "IX_SeasonRankingEntry_SeasonRankingId_TeamRefUrlHash",
                table: "SeasonRankingEntry",
                columns: new[] { "SeasonRankingId", "TeamRefUrlHash" },
                unique: true);
        }
    }
}
