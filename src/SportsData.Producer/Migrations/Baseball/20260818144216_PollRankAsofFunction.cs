using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsData.Producer.Migrations.Baseball
{
    /// <inheritdoc />
    public partial class PollRankAsofFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // THE poll-rank resolver: the single definition every
            // rank-displaying query calls. Finds the poll in effect at
            // p_cutoff (latest published 'ap'/'cfp' poll, 'cfp' preferred on
            // ties as the successor to ESPN's DefaultRanking notion), then the
            // team's ranked entry in it - or NULL, honestly unranked. Keyed on
            // publish date; SeasonPollWeek.SeasonWeekId is never consulted
            // (those links are unreliable: off-by-one late season, NULL at the
            // season's ends).
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.poll_rank_asof(uuid, integer, timestamp with time zone);");
        }
    }
}
