-- Task 06: the worked example (Tamarcus Cooley's interception, play bb0fb521-...) under both document rules,
-- and the timing of the season-document load the handler runs (same shape as LoadSeasonStats).
-- Database: sdProducer.FootballNcaa (local, prod restore).

-- A. Season document(s) for the interceptor vs kickoff.
SELECT s."Id", s."SplitType", s."CreatedUtc", ct."StartDateUtc" AS kickoff,
       s."CreatedUtc" < ct."StartDateUtc" AS before_kickoff, st."Value" AS season_interceptions
FROM "CompetitionPlay" p
JOIN "Competition" co ON co."Id" = p."CompetitionId"
JOIN "Contest" ct ON ct."Id" = co."ContestId"
JOIN "CompetitionPlayParticipant" pp ON pp."CompetitionPlayId" = p."Id" AND pp."Type" = 'passDefender'
JOIN "AthleteSeasonStatistic" s ON s."AthleteSeasonId" = pp."AthleteSeasonId" AND s."SplitId" = '0'
JOIN "AthleteSeasonStatisticCategory" c ON c."AthleteSeasonStatisticId" = s."Id" AND c."Name" = 'defensiveInterceptions'
JOIN "AthleteSeasonStatisticStat" st ON st."AthleteSeasonStatisticCategoryId" = c."Id" AND st."Name" = 'interceptions'
WHERE p."Id" = 'bb0fb521-bf2f-08f5-afb0-97693bed4a07'
ORDER BY s."CreatedUtc";

-- B. Timing of the loader query for this play's athletes.
EXPLAIN (ANALYZE, BUFFERS, SUMMARY)
SELECT s."AthleteSeasonId", s."Id", s."CreatedUtc", c."Name", st."Name", st."Value"
FROM "AthleteSeasonStatistic" s
JOIN "AthleteSeasonStatisticCategory" c ON c."AthleteSeasonStatisticId" = s."Id"
JOIN "AthleteSeasonStatisticStat" st ON st."AthleteSeasonStatisticCategoryId" = c."Id"
WHERE s."SplitId" = '0'
  AND s."AthleteSeasonId" IN (SELECT pp."AthleteSeasonId" FROM "CompetitionPlayParticipant" pp
                              WHERE pp."CompetitionPlayId" = 'bb0fb521-bf2f-08f5-afb0-97693bed4a07')
  AND c."Name" IN ('defensiveInterceptions','defensive','general','passing','receiving','rushing')
  AND st."Name" IN ('interceptions','sacks','fumblesRecovered','passingTouchdowns','receivingTouchdowns',
                    'rushingTouchdowns','fumblesLost','longRushing','longPassing','longReception')
  AND st."Value" IS NOT NULL;
