-- Task 01 (data spike) — how current is AthleteSeasonStatistic, and can it double-count a live game?
-- Database: sdProducer.FootballNcaa (local). Stat: defensiveInterceptions / interceptions.

-- A. Mid-game writes: season-stat documents written WHILE one of the athlete's games was in progress
--    (between kickoff and finalization). These could already include part of that game.
WITH games AS (
    SELECT ac."AthleteSeasonId", ct."StartDateUtc" AS kickoff, ct."FinalizedUtc" AS finalized
    FROM "AthleteCompetition" ac
    JOIN "Competition" co ON co."Id" = ac."CompetitionId"
    JOIN "Contest" ct ON ct."Id" = co."ContestId"
    WHERE ct."SeasonYear" = 2026 AND ct."FinalizedUtc" IS NOT NULL
)
SELECT count(*) AS season_docs_2026,
       count(*) FILTER (WHERE EXISTS (
           SELECT 1 FROM games g
           WHERE g."AthleteSeasonId" = s."AthleteSeasonId"
             AND s."CreatedUtc" BETWEEN g.kickoff AND g.finalized)) AS written_mid_game
FROM "AthleteSeasonStatistic" s
WHERE s."AthleteSeasonId" IN (SELECT "AthleteSeasonId" FROM games);

-- B. Agreement: newest season value vs the sum of per-game values from games finalized before it was written.
WITH newest AS (
    SELECT DISTINCT ON (s."AthleteSeasonId") s."AthleteSeasonId", s."CreatedUtc", st."Value" AS season_value
    FROM "AthleteSeasonStatistic" s
    JOIN "AthleteSeasonStatisticCategory" c ON c."AthleteSeasonStatisticId" = s."Id" AND c."Name" = 'defensiveInterceptions'
    JOIN "AthleteSeasonStatisticStat" st ON st."AthleteSeasonStatisticCategoryId" = c."Id" AND st."Name" = 'interceptions'
    JOIN "AthleteSeason" a ON a."Id" = s."AthleteSeasonId"
    WHERE s."SplitId" = '0'
      AND s."AthleteSeasonId" IN (
          SELECT ac."AthleteSeasonId" FROM "AthleteCompetition" ac
          JOIN "Competition" co ON co."Id" = ac."CompetitionId"
          JOIN "Contest" ct ON ct."Id" = co."ContestId" WHERE ct."SeasonYear" = 2026)
    ORDER BY s."AthleteSeasonId", s."CreatedUtc" DESC
),
summed AS (
    SELECT n."AthleteSeasonId", n.season_value,
           coalesce(sum(st."Value") FILTER (WHERE ct."FinalizedUtc" < n."CreatedUtc"), 0) AS games_before,
           coalesce(sum(st."Value"), 0) AS games_all
    FROM newest n
    LEFT JOIN "AthleteCompetitionStatistic" s ON s."AthleteSeasonId" = n."AthleteSeasonId"
    LEFT JOIN "Competition" co ON co."Id" = s."CompetitionId"
    LEFT JOIN "Contest" ct ON ct."Id" = co."ContestId" AND ct."SeasonYear" = 2026
    LEFT JOIN "AthleteCompetitionStatisticCategory" c ON c."AthleteCompetitionStatisticId" = s."Id" AND c."Name" = 'defensiveInterceptions'
    LEFT JOIN "AthleteCompetitionStatisticStat" st ON st."AthleteCompetitionStatisticCategoryId" = c."Id" AND st."Name" = 'interceptions'
    GROUP BY n."AthleteSeasonId", n.season_value
)
SELECT count(*) AS athletes,
       count(*) FILTER (WHERE season_value = games_before) AS equals_games_before_write,
       count(*) FILTER (WHERE season_value = games_all)    AS equals_all_games_now,
       count(*) FILTER (WHERE season_value < games_all)    AS season_behind_games,
       count(*) FILTER (WHERE season_value > games_all)    AS season_ahead_of_games
FROM summed
WHERE season_value > 0 OR games_all > 0;
