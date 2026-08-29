using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class ExternalIdSourceUrlHashIndexes : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Raw IF NOT EXISTS rather than CreateIndex: prod got these same
        /// indexes via CREATE INDEX CONCURRENTLY on 2026-08-29 (emergency
        /// remediation for the game-day seq-scan saturation), so the
        /// migration must be a no-op where an index already exists.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_VenueExternalId_SourceUrlHash"" ON public.""VenueExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_SeasonWeekExternalId_SourceUrlHash"" ON public.""SeasonWeekExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_SeasonPollWeekExternalId_SourceUrlHash"" ON public.""SeasonPollWeekExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_SeasonPollExternalId_SourceUrlHash"" ON public.""SeasonPollExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_SeasonPhaseExternalId_SourceUrlHash"" ON public.""SeasonPhaseExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_SeasonFutureExternalId_SourceUrlHash"" ON public.""SeasonFutureExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_SeasonExternalId_SourceUrlHash"" ON public.""SeasonExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_GroupSeasonExternalId_SourceUrlHash"" ON public.""GroupSeasonExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_FranchiseSeasonRankingExternalId_SourceUrlHash"" ON public.""FranchiseSeasonRankingExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_FranchiseSeasonExternalId_SourceUrlHash"" ON public.""FranchiseSeasonExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_FranchiseExternalId_SourceUrlHash"" ON public.""FranchiseExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_ContestExternalId_SourceUrlHash"" ON public.""ContestExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_CompetitionStatusExternalId_SourceUrlHash"" ON public.""CompetitionStatusExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_CompetitionProbabilityExternalId_SourceUrlHash"" ON public.""CompetitionProbabilityExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_CompetitionPowerIndexExternalId_SourceUrlHash"" ON public.""CompetitionPowerIndexExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_CompetitionPlayExternalId_SourceUrlHash"" ON public.""CompetitionPlayExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_CompetitionOddsExternalId_SourceUrlHash"" ON public.""CompetitionOddsExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_CompetitionExternalId_SourceUrlHash"" ON public.""CompetitionExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_CompetitionDriveExternalId_SourceUrlHash"" ON public.""CompetitionDriveExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_CompetitionCompetitorScoreExternalIds_SourceUrlHash"" ON public.""CompetitionCompetitorScoreExternalIds"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_CompetitionCompetitorLineScoreExternalId_SourceUrlHash"" ON public.""CompetitionCompetitorLineScoreExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_CompetitionCompetitorExternalIds_SourceUrlHash"" ON public.""CompetitionCompetitorExternalIds"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_CoachSeasonRecordExternalId_SourceUrlHash"" ON public.""CoachSeasonRecordExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_CoachRecordExternalId_SourceUrlHash"" ON public.""CoachRecordExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_CoachExternalId_SourceUrlHash"" ON public.""CoachExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_AwardExternalId_SourceUrlHash"" ON public.""AwardExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_AthleteSeasonExternalId_SourceUrlHash"" ON public.""AthleteSeasonExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_AthletePositionExternalId_SourceUrlHash"" ON public.""AthletePositionExternalId"" (""SourceUrlHash"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_AthleteExternalId_SourceUrlHash"" ON public.""AthleteExternalId"" (""SourceUrlHash"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VenueExternalId_SourceUrlHash",
                table: "VenueExternalId");

            migrationBuilder.DropIndex(
                name: "IX_SeasonWeekExternalId_SourceUrlHash",
                table: "SeasonWeekExternalId");

            migrationBuilder.DropIndex(
                name: "IX_SeasonPollWeekExternalId_SourceUrlHash",
                table: "SeasonPollWeekExternalId");

            migrationBuilder.DropIndex(
                name: "IX_SeasonPollExternalId_SourceUrlHash",
                table: "SeasonPollExternalId");

            migrationBuilder.DropIndex(
                name: "IX_SeasonPhaseExternalId_SourceUrlHash",
                table: "SeasonPhaseExternalId");

            migrationBuilder.DropIndex(
                name: "IX_SeasonFutureExternalId_SourceUrlHash",
                table: "SeasonFutureExternalId");

            migrationBuilder.DropIndex(
                name: "IX_SeasonExternalId_SourceUrlHash",
                table: "SeasonExternalId");

            migrationBuilder.DropIndex(
                name: "IX_GroupSeasonExternalId_SourceUrlHash",
                table: "GroupSeasonExternalId");

            migrationBuilder.DropIndex(
                name: "IX_FranchiseSeasonRankingExternalId_SourceUrlHash",
                table: "FranchiseSeasonRankingExternalId");

            migrationBuilder.DropIndex(
                name: "IX_FranchiseSeasonExternalId_SourceUrlHash",
                table: "FranchiseSeasonExternalId");

            migrationBuilder.DropIndex(
                name: "IX_FranchiseExternalId_SourceUrlHash",
                table: "FranchiseExternalId");

            migrationBuilder.DropIndex(
                name: "IX_ContestExternalId_SourceUrlHash",
                table: "ContestExternalId");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionStatusExternalId_SourceUrlHash",
                table: "CompetitionStatusExternalId");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionProbabilityExternalId_SourceUrlHash",
                table: "CompetitionProbabilityExternalId");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionPowerIndexExternalId_SourceUrlHash",
                table: "CompetitionPowerIndexExternalId");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionPlayExternalId_SourceUrlHash",
                table: "CompetitionPlayExternalId");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionOddsExternalId_SourceUrlHash",
                table: "CompetitionOddsExternalId");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionExternalId_SourceUrlHash",
                table: "CompetitionExternalId");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionDriveExternalId_SourceUrlHash",
                table: "CompetitionDriveExternalId");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionCompetitorScoreExternalIds_SourceUrlHash",
                table: "CompetitionCompetitorScoreExternalIds");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionCompetitorLineScoreExternalId_SourceUrlHash",
                table: "CompetitionCompetitorLineScoreExternalId");

            migrationBuilder.DropIndex(
                name: "IX_CompetitionCompetitorExternalIds_SourceUrlHash",
                table: "CompetitionCompetitorExternalIds");

            migrationBuilder.DropIndex(
                name: "IX_CoachSeasonRecordExternalId_SourceUrlHash",
                table: "CoachSeasonRecordExternalId");

            migrationBuilder.DropIndex(
                name: "IX_CoachRecordExternalId_SourceUrlHash",
                table: "CoachRecordExternalId");

            migrationBuilder.DropIndex(
                name: "IX_CoachExternalId_SourceUrlHash",
                table: "CoachExternalId");

            migrationBuilder.DropIndex(
                name: "IX_AwardExternalId_SourceUrlHash",
                table: "AwardExternalId");

            migrationBuilder.DropIndex(
                name: "IX_AthleteSeasonExternalId_SourceUrlHash",
                table: "AthleteSeasonExternalId");

            migrationBuilder.DropIndex(
                name: "IX_AthletePositionExternalId_SourceUrlHash",
                table: "AthletePositionExternalId");

            migrationBuilder.DropIndex(
                name: "IX_AthleteExternalId_SourceUrlHash",
                table: "AthleteExternalId");
        }
    }
}
