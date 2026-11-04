using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Football
{
    /// <inheritdoc />
    public partial class _04NovV1_PollRankAsofCfpPrecedence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CFP precedence by availability. The original resolver took the
            // latest-published ap/cfp poll, so every Sunday's AP poll
            // displaced Tuesday's CFP rankings until the next CFP release:
            // matchups generated Sunday-Tuesday carried AP ranks and the
            // Top 25 filter admitted AP-only teams. Now: once the season has
            // a CFP poll published at or before p_cutoff, the latest such CFP
            // poll is the poll in effect; before the first CFP release, the
            // latest AP poll. No dates or season literals - the switch
            // happens on its own whenever CFP first publishes.
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION public.poll_rank_asof(
    p_franchise_season_id uuid,
    p_season_year integer,
    p_cutoff timestamp with time zone)
RETURNS integer
LANGUAGE sql
STABLE
AS $BODY$
  SELECT spwe."Current"
  FROM public."SeasonPollWeekEntry" spwe
  WHERE spwe."SeasonPollWeekId" = (
      SELECT spw."Id"
      FROM public."SeasonPollWeek" spw
      INNER JOIN public."SeasonPoll" sp ON sp."Id" = spw."SeasonPollId"
      WHERE sp."SeasonYear" = p_season_year
        AND spw."Type" IN ('ap', 'cfp')
        AND spw."DateUtc" IS NOT NULL
        AND spw."DateUtc" <= p_cutoff
      ORDER BY CASE WHEN spw."Type" = 'cfp' THEN 0 ELSE 1 END, spw."DateUtc" DESC
      LIMIT 1)
    AND spwe."FranchiseSeasonId" = p_franchise_season_id
    AND NOT spwe."IsOtherReceivingVotes"
    AND NOT spwe."IsDroppedOut"
  LIMIT 1
$BODY$;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the 20260818144149_PollRankAsofFunction body:
            // latest-published poll wins, cfp preferred only on ties.
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION public.poll_rank_asof(
    p_franchise_season_id uuid,
    p_season_year integer,
    p_cutoff timestamp with time zone)
RETURNS integer
LANGUAGE sql
STABLE
AS $BODY$
  SELECT spwe."Current"
  FROM public."SeasonPollWeekEntry" spwe
  WHERE spwe."SeasonPollWeekId" = (
      SELECT spw."Id"
      FROM public."SeasonPollWeek" spw
      INNER JOIN public."SeasonPoll" sp ON sp."Id" = spw."SeasonPollId"
      WHERE sp."SeasonYear" = p_season_year
        AND spw."Type" IN ('ap', 'cfp')
        AND spw."DateUtc" IS NOT NULL
        AND spw."DateUtc" <= p_cutoff
      ORDER BY spw."DateUtc" DESC, CASE WHEN spw."Type" = 'cfp' THEN 0 ELSE 1 END
      LIMIT 1)
    AND spwe."FranchiseSeasonId" = p_franchise_season_id
    AND NOT spwe."IsOtherReceivingVotes"
    AND NOT spwe."IsDroppedOut"
  LIMIT 1
$BODY$;
""");
        }
    }
}
