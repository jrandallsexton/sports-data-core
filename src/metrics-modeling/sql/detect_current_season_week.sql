-- Resolve "now" to (SeasonYear, WeekNumber) for live runs. Prefers the
-- week window containing NOW(); falls back to the NEXT upcoming week so
-- a run a few days before the window opens (e.g. the Tuesday before
-- kickoff week) still resolves. Preseason weeks excluded (policy).
WITH candidates AS (
    SELECT s."Year" AS "SeasonYear",
           sw."Number" AS "WeekNumber",
           sw."StartDate",
           CASE WHEN sw."StartDate" <= NOW() AND sw."EndDate" > NOW() THEN 0 ELSE 1 END AS pref
    FROM public."Season" s
    JOIN public."SeasonWeek" sw ON sw."SeasonId" = s."Id"
    JOIN public."SeasonPhase" sp ON sp."Id" = sw."SeasonPhaseId"
    WHERE sp."TypeCode" <> 1
      AND sw."EndDate" > NOW()
)
SELECT "SeasonYear", "WeekNumber"
FROM candidates
ORDER BY pref, "StartDate"
LIMIT 1;
