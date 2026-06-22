-- Find finalized NFL contests where Contest.SpreadWinnerFranchiseSeasonId
-- never propagated from the underlying CompetitionOdds row. Each row returned
-- is a candidate for the admin "Re-run Enrichment" button.
--
-- NFL variant of audit_stuck_spread_winner_ncaafb_fbs.sql. NFL has no FBS
-- concept (all franchises are in scope), so the GroupSeason tree walk is
-- replaced with a simple Sport filter (Sport.FootballNfl = 3).
--
-- Run against the Football Producer DB. Replace :seasonYear with the target
-- year (e.g. 2025).
--
-- Detection rule: same as the NCAAFB FBS variant. Finalized contest +
-- SpreadWinnerFranchiseSeasonId IS NULL + at least one enriched
-- CompetitionOdds row has a non-null AtsWinnerFranchiseSeasonId. A push
-- (AtsWinner = Guid.Empty) IS a real result and should propagate — the
-- rule flags those correctly because Guid.Empty is non-null.

SELECT
    con."Id"                                AS "ContestId",
    con."Name"                              AS "ContestName",
    con."SeasonYear",
    con."StartDateUtc",
    con."FinalizedUtc",
    con."WinnerFranchiseSeasonId"           AS "Contest_StraightUpWinner",
    con."SpreadWinnerFranchiseSeasonId"     AS "Contest_SpreadWinner",
    o."ProviderId"                          AS "Odds_ProviderId",
    o."AtsWinnerFranchiseSeasonId"          AS "Odds_AtsWinner",
    o."OverUnderResult"                     AS "Odds_OverUnderResult",
    o."FinalizedUtc"                         AS "Odds_FinalizedUtc"
FROM public."Contest" con
JOIN public."Competition" comp     ON comp."ContestId" = con."Id"
JOIN public."CompetitionOdds" o    ON o."CompetitionId" = comp."Id"
WHERE con."SeasonYear" = :seasonYear
  AND con."Sport" = 3                    -- Sport.FootballNfl
  AND con."FinalizedUtc" IS NOT NULL
  AND con."SpreadWinnerFranchiseSeasonId" IS NULL
  AND o."FinalizedUtc" IS NOT NULL
  AND o."AtsWinnerFranchiseSeasonId" IS NOT NULL
ORDER BY con."StartDateUtc", o."ProviderId";
