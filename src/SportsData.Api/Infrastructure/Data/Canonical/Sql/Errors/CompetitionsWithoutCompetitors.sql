SELECT comp."Id" AS "CompetitionId",
       con."Id" AS "ContestId",
       con."Name" AS "CompetitionName",
       COUNT(cc."Id") AS "CompetitorCount"
FROM public."Competition" comp
JOIN public."Contest" con ON con."Id" = comp."ContestId"
LEFT JOIN public."CompetitionCompetitor" cc ON cc."CompetitionId" = comp."Id"
GROUP BY comp."Id", con."Id", con."Name"
HAVING COUNT(cc."Id") != 2
ORDER BY "CompetitorCount" ASC;