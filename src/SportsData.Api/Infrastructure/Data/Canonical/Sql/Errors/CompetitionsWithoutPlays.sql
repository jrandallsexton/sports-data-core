SELECT 
    con."Id" AS "ContestId",
    con."Name" AS "ContestName",
    con."StartDateUtc",
    comp."Id" AS "CompetitionId",
    COUNT(cp."Id") AS "PlayCount",
    MAX(cp."Text") AS "LastPlayText"
FROM public."Competition" comp
JOIN public."Contest" con ON con."Id" = comp."ContestId"
LEFT JOIN public."CompetitionPlay" cp ON cp."CompetitionId" = comp."Id"
WHERE con."StartDateUtc" < now()  -- ⏰ Only games that should have started
GROUP BY con."Id", con."Name", con."StartDateUtc", comp."Id"
HAVING COUNT(cp."Id") <= 10
ORDER BY con."StartDateUtc";