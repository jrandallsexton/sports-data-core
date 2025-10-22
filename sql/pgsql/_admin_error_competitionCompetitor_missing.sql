select con."Id", con."Name", comp."Id" as "CompId"
from public."Contest" con
inner join public."Competition" comp on comp."ContestId" = con."Id"
where con."Id" = '7f39067b-40bb-aa0b-225d-7670409d1003'

select * from public."CompetitionCompetitor" where "CompetitionId" = 'eda0c287-0d48-4715-4405-51414c3a416b'

SELECT comp."Id" AS "CompetitionId",
       con."Id" AS "ContestId",
       con."Name",
       COUNT(cc."Id") AS "CompetitorCount"
FROM public."Competition" comp
JOIN public."Contest" con ON con."Id" = comp."ContestId"
LEFT JOIN public."CompetitionCompetitor" cc ON cc."CompetitionId" = comp."Id"
GROUP BY comp."Id", con."Id", con."Name"
HAVING COUNT(cc."Id") != 2
ORDER BY "CompetitorCount" ASC;
