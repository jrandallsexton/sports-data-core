SELECT 
    sw."Id" AS "Id",
    sp."Name" AS "SeasonPhase",
    sw."Number" AS "WeekNumber",
    s."Id" AS "SeasonId",
    s."Year" AS "SeasonYear",
    sw."StartDate",
    sw."EndDate"
FROM public."Season" s
JOIN public."SeasonWeek" sw ON sw."SeasonId" = s."Id"
JOIN public."SeasonPhase" sp ON sp."Id" = sw."SeasonPhaseId"
WHERE
  (
    (sw."StartDate" <= NOW() AND sw."EndDate" > NOW()) -- current week
    OR
    (sw."StartDate" >= NOW() - INTERVAL '13 days' AND sw."StartDate" < NOW()) -- last week
  )
ORDER BY sw."StartDate" DESC
LIMIT 2;