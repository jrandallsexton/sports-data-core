-- The games BEHIND the ATS bucket count: every decided ATS result for one
-- franchise as a favorite (@AsFavorite = TRUE) or underdog (FALSE) inside
-- the key-number band [@Threshold, @ThresholdUpper) — see
-- GetFranchiseAtsBucket.sql for the band semantics (999 = open-ended top).
-- Newest first, each with the opponent, the team-relative closing spread,
-- whether the team covered, and the opponent's overall record that season.
-- This is what turns "covered 4 of 9" into an argument — a reader can see
-- WHO was covered against (docs/features: matchup-spread-context; owner
-- ask 2026-09-12, same contract as GetFranchiseMarginInstances.sql).
--
-- Same frame as GetFranchiseAtsBucket.sql — the count and this list MUST
-- agree row-for-row: market tier (spread VALUES, ~2022+), decided ATS
-- results only (SpreadWinner null = push or unsourced -> excluded),
-- finalized/non-cancelled, strictly before @AsOf, preseason (TypeCode 1)
-- excluded, NULL phase kept. Capped at 10 rows; the headline count remains
-- the authority on totals.
SELECT
    c."StartDateUtc" AS "GameDate",
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
    -- co."Spread" is home-relative (negative = home favored); flip for the
    -- away side so the value is always THIS team's line.
    CASE WHEN fsHome."FranchiseId" = @FranchiseId
         THEN co."Spread" ELSE -co."Spread"
    END AS "TeamSpread",
    CASE WHEN fsHome."FranchiseId" = @FranchiseId
         THEN c."SpreadWinnerFranchiseSeasonId" = c."HomeTeamFranchiseSeasonId"
         ELSE c."SpreadWinnerFranchiseSeasonId" = c."AwayTeamFranchiseSeasonId"
    END AS "Covered",
    rec."OpponentSeasonRecord"
FROM public."Contest" c
INNER JOIN public."Competition" comp ON comp."ContestId" = c."Id"
INNER JOIN LATERAL (
    SELECT *
    FROM public."CompetitionOdds"
    WHERE "CompetitionId" = comp."Id"
      AND "ProviderId" IN ('{PreferredOddsProviderId}', '{FallbackOddsProviderId}')
      AND "Spread" IS NOT NULL
    ORDER BY CASE WHEN "ProviderId" = '{PreferredOddsProviderId}' THEN 1 ELSE 2 END
    LIMIT 1
) co ON TRUE
INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
LEFT JOIN public."SeasonPhase" sp ON sp."Id" = c."SeasonPhaseId"
LEFT JOIN LATERAL (
    -- Opponent's overall record THAT season, "W-L". Same lateral as
    -- GetFranchiseMarginInstances.sql (FranchiseSeasonRecord Type='total',
    -- stats 'wins'/'losses'); absent stays NULL — never a fabricated 0-0.
    SELECT CONCAT(
        MAX(CASE WHEN st."Name" = 'wins'   THEN st."Value"::int END),
        '-',
        MAX(CASE WHEN st."Name" = 'losses' THEN st."Value"::int END)
    ) AS "OpponentSeasonRecord"
    FROM public."FranchiseSeasonRecord" r
    INNER JOIN public."FranchiseSeasonRecordStat" st
        ON st."FranchiseSeasonRecordId" = r."Id"
    WHERE r."FranchiseSeasonId" = CASE WHEN fsHome."FranchiseId" = @FranchiseId
                                       THEN c."AwayTeamFranchiseSeasonId"
                                       ELSE c."HomeTeamFranchiseSeasonId"
                                  END
      AND r."Type" = 'total'
    -- Both stats or nothing (feature honesty rule): a missing side must
    -- yield NULL, never a half-record like "12-".
    HAVING MAX(CASE WHEN st."Name" = 'wins'   THEN st."Value"::int END) IS NOT NULL
       AND MAX(CASE WHEN st."Name" = 'losses' THEN st."Value"::int END) IS NOT NULL
) rec ON TRUE
WHERE c."FinalizedUtc" IS NOT NULL
  AND c."CancelledUtc" IS NULL
  AND c."StartDateUtc" < @AsOf
  AND (sp."TypeCode" IS NULL OR sp."TypeCode" <> 1)
  AND c."SpreadWinnerFranchiseSeasonId" IS NOT NULL
  AND (
        (fsHome."FranchiseId" = @FranchiseId AND ((@AsFavorite AND co."Spread" <= -@Threshold AND co."Spread" > -@ThresholdUpper) OR (NOT @AsFavorite AND co."Spread" >= @Threshold AND co."Spread" < @ThresholdUpper)))
     OR (fsAway."FranchiseId" = @FranchiseId AND ((@AsFavorite AND co."Spread" >= @Threshold AND co."Spread" < @ThresholdUpper) OR (NOT @AsFavorite AND co."Spread" <= -@Threshold AND co."Spread" > -@ThresholdUpper)))
  )
ORDER BY c."StartDateUtc" DESC
LIMIT 10
