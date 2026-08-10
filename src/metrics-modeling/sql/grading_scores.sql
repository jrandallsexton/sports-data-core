-- Final scores for grading a backtested week: the (season, week) slate's
-- completed games. Same slate filters as the as-of extraction (preseason
-- and cancelled games excluded) so the join back to predictions is
-- one-to-one; contests that never finished simply don't appear here and
-- are reported as ungradeable.
--
-- psql -v season_year=2025 -v week=6
SELECT
    con."Id" AS "ContestId",
    con."HomeScore",
    con."AwayScore"
FROM public."Season" s
JOIN public."SeasonWeek" sw ON sw."SeasonId" = s."Id" AND sw."Number" = :week::int
JOIN public."Contest" con   ON con."SeasonWeekId" = sw."Id"
LEFT JOIN public."SeasonPhase" sp ON sp."Id" = con."SeasonPhaseId"
WHERE s."Year" = :season_year::int
  AND con."HomeScore" IS NOT NULL
  AND con."AwayScore" IS NOT NULL
  AND con."CancelledUtc" IS NULL
  AND (sp."TypeCode" IS NULL OR sp."TypeCode" <> 1);
