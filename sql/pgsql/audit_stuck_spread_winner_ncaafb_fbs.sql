-- Find finalized NCAAFB FBS contests where Contest.SpreadWinnerFranchiseSeasonId
-- never propagated from the underlying CompetitionOdds row. Each row returned
-- is a candidate for the admin "Re-run Enrichment" button — the same recovery
-- path that flipped the first observed case on 2026-06-21.
--
-- Run against the Football Producer DB. Replace :seasonYear with the target
-- year (e.g. 2024). For multiple seasons, sweep them individually.
--
-- Detection rule: contest is Final but SpreadWinnerFranchiseSeasonId IS NULL,
-- AND there's at least one enriched CompetitionOdds row for the underlying
-- Competition with a non-null AtsWinnerFranchiseSeasonId. That's the
-- "enrichment derived an ATS winner but didn't write it back to Contest"
-- signature. A Guid.Empty AtsWinner (push) is a real result and SHOULD
-- propagate; the Contest NULL means it didn't.

WITH RECURSIVE gs_tree AS (
    -- FBS group tree for the season (mirrors errors_CompetitionsWithoutMetrics.sql)
    SELECT gs."Id", gs."ParentId", gs."Slug", gs."SeasonYear"
    FROM public."GroupSeason" gs
    WHERE gs."Slug" = 'fbs-i-a'
      AND gs."SeasonYear" = :seasonYear

    UNION ALL

    SELECT child."Id", child."ParentId", child."Slug", child."SeasonYear"
    FROM public."GroupSeason" child
    JOIN gs_tree parent ON child."ParentId" = parent."Id"
),
fbs_fs AS (
    SELECT fs."Id" AS "FranchiseSeasonId"
    FROM public."FranchiseSeason" fs
    JOIN gs_tree g ON fs."GroupSeasonId" = g."Id"
    WHERE fs."SeasonYear" = :seasonYear
),
fbs_competitions AS (
    SELECT DISTINCT cc."CompetitionId"
    FROM public."CompetitionCompetitor" cc
    JOIN fbs_fs fbs ON cc."FranchiseSeasonId" = fbs."FranchiseSeasonId"
)
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
    o."EnrichedUtc"                         AS "Odds_EnrichedUtc"
FROM public."Contest" con
JOIN public."Competition" comp     ON comp."ContestId" = con."Id"
JOIN fbs_competitions fc           ON fc."CompetitionId" = comp."Id"
JOIN public."CompetitionOdds" o    ON o."CompetitionId" = comp."Id"
WHERE con."SeasonYear" = :seasonYear
  AND con."FinalizedUtc" IS NOT NULL
  AND con."SpreadWinnerFranchiseSeasonId" IS NULL
  AND o."EnrichedUtc" IS NOT NULL
  AND o."AtsWinnerFranchiseSeasonId" IS NOT NULL
ORDER BY con."StartDateUtc", o."ProviderId";
