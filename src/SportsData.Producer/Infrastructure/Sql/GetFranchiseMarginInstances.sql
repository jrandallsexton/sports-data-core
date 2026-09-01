-- The games BEHIND the margin fact's count: every qualifying win/loss by
-- >= @Margin inside the five-season window, newest first, each with the
-- opponent and the opponent's overall record that season. This is what
-- turns "8 such wins in the last 5 seasons" into an argument — a reader
-- can see WHO was actually beaten (docs/features: spread-contextualized
-- history; owner ask 2026-09-01).
--
-- Same preview-safe frame as GetFranchiseMarginFact.sql: finalized,
-- non-cancelled, strictly before @AsOf, preseason (TypeCode 1) excluded.
-- Capped at 10 rows; the headline count remains the authority on totals.
WITH franchise_games AS (
    SELECT
        c."StartDateUtc",
        c."SeasonYear",
        CASE WHEN fsHome."FranchiseId" = @FranchiseId
             THEN fAway."DisplayName" ELSE fHome."DisplayName"
        END AS "Opponent",
        CASE WHEN fsHome."FranchiseId" = @FranchiseId
             THEN c."HomeScore" ELSE c."AwayScore"
        END AS "TeamScore",
        CASE WHEN fsHome."FranchiseId" = @FranchiseId
             THEN c."AwayScore" ELSE c."HomeScore"
        END AS "OpponentScore",
        CASE WHEN fsHome."FranchiseId" = @FranchiseId
             THEN c."AwayTeamFranchiseSeasonId"
             ELSE c."HomeTeamFranchiseSeasonId"
        END AS "OpponentFranchiseSeasonId",
        CASE WHEN fsHome."FranchiseId" = @FranchiseId
             THEN c."HomeScore" - c."AwayScore"
             ELSE c."AwayScore" - c."HomeScore"
        END AS "OurMargin"
    FROM public."Contest" c
    INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
    INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
    INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
    INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
    LEFT JOIN public."SeasonPhase" sp ON sp."Id" = c."SeasonPhaseId"
    WHERE (fsHome."FranchiseId" = @FranchiseId OR fsAway."FranchiseId" = @FranchiseId)
      AND c."FinalizedUtc" IS NOT NULL
      AND c."CancelledUtc" IS NULL
      AND c."HomeScore" IS NOT NULL
      AND c."AwayScore" IS NOT NULL
      AND c."StartDateUtc" < @AsOf
      AND (sp."TypeCode" IS NULL OR sp."TypeCode" <> 1)
)
SELECT
    fg."StartDateUtc" AS "GameDate",
    fg."SeasonYear",
    fg."Opponent",
    fg."TeamScore",
    fg."OpponentScore",
    rec."OpponentSeasonRecord"
FROM franchise_games fg
LEFT JOIN LATERAL (
    -- Opponent's overall record THAT season, "W-L". Mirrors the C#
    -- GetOverallRecordAsync (FranchiseSeasonRecord Type='total', stats
    -- 'wins'/'losses'); absent stays NULL — never a fabricated 0-0.
    SELECT CONCAT(
        MAX(CASE WHEN st."Name" = 'wins'   THEN st."Value"::int END),
        '-',
        MAX(CASE WHEN st."Name" = 'losses' THEN st."Value"::int END)
    ) AS "OpponentSeasonRecord"
    FROM public."FranchiseSeasonRecord" r
    INNER JOIN public."FranchiseSeasonRecordStat" st
        ON st."FranchiseSeasonRecordId" = r."Id"
    WHERE r."FranchiseSeasonId" = fg."OpponentFranchiseSeasonId"
      AND r."Type" = 'total'
    -- Both stats or nothing (feature honesty rule): a missing side must
    -- yield NULL, never a half-record like "12-".
    HAVING MAX(CASE WHEN st."Name" = 'wins'   THEN st."Value"::int END) IS NOT NULL
       AND MAX(CASE WHEN st."Name" = 'losses' THEN st."Value"::int END) IS NOT NULL
) rec ON TRUE
WHERE fg."SeasonYear" >= @WindowStartSeason
  AND ((@Won AND fg."OurMargin" >= @Margin)
    OR (NOT @Won AND fg."OurMargin" <= -@Margin))
ORDER BY fg."StartDateUtc" DESC
LIMIT 10
