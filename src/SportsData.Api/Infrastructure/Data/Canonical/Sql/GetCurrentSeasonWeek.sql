SELECT 
    sw."Id" AS "Id",
    sw."Number" AS "WeekNumber",
    s."Id" AS "SeasonId",
    s."Year" AS "SeasonYear",
    sw."IsNonStandardWeek" AS "IsNonStandardWeek"
FROM public."Season" s
JOIN public."SeasonWeek" sw ON sw."SeasonId" = s."Id"
JOIN public."SeasonPhase" sp ON sp."Id" = sw."SeasonPhaseId"
WHERE
  sp."Name" = 'Regular Season' AND
  sw."StartDate" <= NOW() AND
  sw."EndDate" > NOW()
ORDER BY sw."StartDate"
LIMIT 1;