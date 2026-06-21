-- Find finalized NCAAFB FBS contests where Contest.OverUnder never
-- propagated from the underlying CompetitionOdds row. Each row returned
-- is a candidate for the admin "Re-run Enrichment" button.
--
-- Companion to audit_stuck_spread_winner_ncaafb_fbs.sql. Same enrichment
-- processor wrote both fields in one pass —
--
--     contest.OverUnder = primaryOdds.OverUnderResult;
--     contest.SpreadWinnerFranchiseSeasonId = primaryOdds.AtsWinnerFranchiseSeasonId;
--
-- so historically the two drift cases tend to come paired. Run both audits
-- and reconcile the union into a single re-enrich pass per ContestId.
--
-- Detection rule: finalized contest + Contest.OverUnder = 0 (None) +
-- at least one enriched CompetitionOdds row has a non-zero OverUnderResult.
-- The OverUnderResult enum values are: 0=None, 1=Over, 2=Under, 3=Push.
-- Push is a real result and SHOULD propagate; the Contest = 0 means it
-- didn't.
--
-- Run against the Football Producer DB. Replace :seasonYear with the target
-- year (e.g. 2024).

WITH RECURSIVE gs_tree AS (
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
    con."AwayScore",
    con."HomeScore",
    con."OverUnder"                         AS "Contest_OverUnder",
    o."ProviderId"                          AS "Odds_ProviderId",
    o."OverUnder"                           AS "Odds_OverUnderLine",
    o."OverUnderResult"                     AS "Odds_OverUnderResult",
    o."EnrichedUtc"                         AS "Odds_EnrichedUtc"
FROM public."Contest" con
JOIN public."Competition" comp     ON comp."ContestId" = con."Id"
JOIN fbs_competitions fc           ON fc."CompetitionId" = comp."Id"
JOIN public."CompetitionOdds" o    ON o."CompetitionId" = comp."Id"
WHERE con."SeasonYear" = :seasonYear
  AND con."FinalizedUtc" IS NOT NULL
  AND con."OverUnder" = 0                  -- OverUnderResult.None
  AND o."EnrichedUtc" IS NOT NULL
  AND o."OverUnderResult" <> 0             -- enrichment derived a real value
ORDER BY con."StartDateUtc", o."ProviderId";
