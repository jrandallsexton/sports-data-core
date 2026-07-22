using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _22JulV1_SeasonFutureBookAthleteSeason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SeasonFutureBook_FranchiseSeason_FranchiseSeasonId",
                table: "SeasonFutureBook");

            migrationBuilder.AlterColumn<Guid>(
                name: "FranchiseSeasonId",
                table: "SeasonFutureBook",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "AthleteSeasonId",
                table: "SeasonFutureBook",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeasonFutureBook_AthleteSeasonId",
                table: "SeasonFutureBook",
                column: "AthleteSeasonId");

            migrationBuilder.AddForeignKey(
                name: "FK_SeasonFutureBook_AthleteSeason_AthleteSeasonId",
                table: "SeasonFutureBook",
                column: "AthleteSeasonId",
                principalTable: "AthleteSeason",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SeasonFutureBook_FranchiseSeason_FranchiseSeasonId",
                table: "SeasonFutureBook",
                column: "FranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Athlete-only books (AthleteSeasonId set, no team) cannot be
            // represented in the pre-migration schema. Remove them before
            // restoring the non-null FranchiseSeason FK below — otherwise the
            // AlterColumn would coerce their null FranchiseSeasonId to Guid.Empty
            // and the AddForeignKey would fail (no such FranchiseSeason). This
            // makes the downgrade intentionally lossy for athlete-market futures.
            migrationBuilder.Sql(
                "DELETE FROM \"SeasonFutureBook\" WHERE \"AthleteSeasonId\" IS NOT NULL AND \"FranchiseSeasonId\" IS NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_SeasonFutureBook_AthleteSeason_AthleteSeasonId",
                table: "SeasonFutureBook");

            migrationBuilder.DropForeignKey(
                name: "FK_SeasonFutureBook_FranchiseSeason_FranchiseSeasonId",
                table: "SeasonFutureBook");

            migrationBuilder.DropIndex(
                name: "IX_SeasonFutureBook_AthleteSeasonId",
                table: "SeasonFutureBook");

            migrationBuilder.DropColumn(
                name: "AthleteSeasonId",
                table: "SeasonFutureBook");

            migrationBuilder.AlterColumn<Guid>(
                name: "FranchiseSeasonId",
                table: "SeasonFutureBook",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SeasonFutureBook_FranchiseSeason_FranchiseSeasonId",
                table: "SeasonFutureBook",
                column: "FranchiseSeasonId",
                principalTable: "FranchiseSeason",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
