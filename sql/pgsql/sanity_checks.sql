SELECT fs."Id", fs."FranchiseId"
FROM "FranchiseSeason" fs
LEFT JOIN "Franchise" f ON f."Id" = fs."FranchiseId"
WHERE f."Id" IS NULL;

SELECT c."Id", c."FranchiseSeasonId"
FROM "FranchiseSeasonStatisticCategory" c
LEFT JOIN "FranchiseSeason" fs ON fs."Id" = c."FranchiseSeasonId"
WHERE fs."Id" IS NULL;

SELECT a."Id", a."Name", fsa."Id" AS "AwardSeasonId"
FROM "Award" a
JOIN "FranchiseSeasonAward" fsa ON fsa."AwardId" = a."Id"
LEFT JOIN "FranchiseSeasonAwardWinner" fsaw ON fsaw."FranchiseSeasonAwardId" = fsa."Id"
WHERE fsaw."Id" IS NULL;


