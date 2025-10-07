WITH base AS (
  SELECT
      co."ContestId",
      cp."CompetitionId",
      cp."SequenceNumber"            AS "Ordinal",
      cp."PeriodNumber"              AS "Quarter",
      cp."ClockDisplayValue"         AS "TimeRemaining",
      f."Name"                       AS "Team",
      cp."StartDown"                 AS "Down",
      cp."StartDistance"             AS "ToGo",
      pt."Name"                      AS "PlayType",
      cp."Text"                      AS "Description",

      cp."StartYardLine",
      cp."EndYardLine",
      /* Prefer explicit YTE columns if you have them; otherwise derive as (100 - yardline)
         because ESPN's yardline increases toward the opponent end zone. */
      COALESCE(cp."StartYardsToEndzone", 100 - cp."StartYardLine")::int AS start_yte,
      COALESCE(cp."EndYardsToEndzone",   100 - cp."EndYardLine")::int   AS end_yte,

      cp."StatYardage"               AS "Yards"
  FROM public."CompetitionPlay" cp
  JOIN public."lkPlayType" pt   ON pt."Id" = cp."Type"
  JOIN public."Competition" co  ON co."Id" = cp."CompetitionId"
  JOIN public."Contest" c       ON c."Id" = co."ContestId"
  JOIN public."FranchiseSeason" fs ON fs."Id" = cp."StartTeamFranchiseSeasonId"
  JOIN public."Franchise" f        ON f."Id" = fs."FranchiseId"
  -- Optional while testing:
  WHERE co."ContestId" = 'b6cde160-f48d-9d51-784b-56bf4adb990a'
),
scrimmage AS (
  /* Limit to true from-scrimmage plays. Adjust names to your lkPlayType taxonomy. */
  SELECT *
  FROM base
  WHERE "PlayType" IN (
    'rush','rushAttempt','passReception','passComplete',
    'passIncomplete','sack','scramble','kneel','spike'
  )
  AND "Description" NOT ILIKE '%NO PLAY%'
),
calc AS (
  SELECT
    *,
    /* Positive delta_yte means offense gained yards (start_yte > end_yte) */
    (start_yte - end_yte)                 AS delta_yte,
    ABS(ABS(start_yte - end_yte) - ABS("Yards")) AS mag_diff,
    CASE
      WHEN "Yards" = 0 OR (start_yte - end_yte) = 0 THEN FALSE
      ELSE (("Yards" > 0) AND (start_yte - end_yte) < 0)
        OR (("Yards" < 0) AND (start_yte - end_yte) > 0)
    END AS sign_mismatch
  FROM scrimmage
)
SELECT
  "ContestId",
  "CompetitionId",
  "Ordinal",
  "Quarter",
  "TimeRemaining",
  "Team",
  "Down",
  "ToGo",
  "PlayType",
  "Description",
  "StartYardLine" AS "YardLine",
  "EndYardLine",
  "Yards"         AS "StatYardage",
  (start_yte - end_yte) AS "Delta_YardsToEndzone",
  mag_diff,
  sign_mismatch
FROM calc
WHERE sign_mismatch = TRUE
  AND mag_diff <= 0.5   -- exact match? use = 0; allow small tolerance for bookkeeping/rounding
ORDER BY "ContestId","CompetitionId","Ordinal";
