-- Find finalized NFL contests where Contest.OverUnder never propagated from
-- the underlying CompetitionOdds row. Each row returned is a candidate for
-- the admin "Re-run Enrichment" button.
--
-- NFL variant of audit_stuck_overunder_ncaafb_fbs.sql. NFL has no FBS
-- concept; replaces the GroupSeason tree walk with Sport.FootballNfl = 3.
--
-- Detection rule: finalized contest + Contest.OverUnder = 0 (None) +
-- at least one enriched CompetitionOdds row has a non-zero OverUnderResult.
-- Enum: 0=None, 1=Over, 2=Under, 3=Push. Push IS a real result and should
-- propagate.
--
-- Run against the Football Producer DB. Replace :seasonYear with the target
-- year (e.g. 2025).

SELECT
    con."Id"                                AS "ContestId",
    con."Name"                              AS "ContestName",
    con."SeasonYear",
    con."StartDateUtc",
    con."FinalizedUtc",
    con."AwayScore",
    con."HomeScore",
    con."OverUnder"                         AS "Contest_OverUnder",
    o."ProviderId"                          AS "Odds_ProviderId",
    o."OverUnder"                           AS "Odds_OverUnderLine",
    o."OverUnderResult"                     AS "Odds_OverUnderResult",
    o."FinalizedUtc"                         AS "Odds_FinalizedUtc"
FROM public."Contest" con
JOIN public."Competition" comp     ON comp."ContestId" = con."Id"
JOIN public."CompetitionOdds" o    ON o."CompetitionId" = comp."Id"
WHERE con."SeasonYear" = :seasonYear
  AND con."Sport" = 3                      -- Sport.FootballNfl
  AND con."FinalizedUtc" IS NOT NULL
  AND con."OverUnder" = 0                  -- OverUnderResult.None
  AND o."FinalizedUtc" IS NOT NULL
  AND o."OverUnderResult" <> 0
ORDER BY con."StartDateUtc", o."ProviderId";
