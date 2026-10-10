-- Task 01 (data spike) — do season-stat documents written MID-GAME already include the in-progress game?
-- For each mid-game document's interception value, compare with the sum from games finalized before it was written.
-- season > before  => the document already counts part of the live game (double-count risk if we add the live game's plays).
WITH games AS (
    SELECT ac."AthleteSeasonId", ac."CompetitionId", ct."StartDateUtc" AS kickoff, ct."FinalizedUtc" AS finalized
    FROM "AthleteCompetition" ac
    JOIN "Competition" co ON co."Id" = ac."CompetitionId"
    JOIN "Contest" ct ON ct."Id" = co."ContestId"
    WHERE ct."SeasonYear" = 2026 AND ct."FinalizedUtc" IS NOT NULL
),
mid AS (
    SELECT s."Id", s."AthleteSeasonId", s."CreatedUtc", g."CompetitionId" AS live_competition, st."Value" AS season_value
    FROM "AthleteSeasonStatistic" s
    JOIN games g ON g."AthleteSeasonId" = s."AthleteSeasonId" AND s."CreatedUtc" BETWEEN g.kickoff AND g.finalized
    JOIN "AthleteSeasonStatisticCategory" c ON c."AthleteSeasonStatisticId" = s."Id" AND c."Name" = 'defensiveInterceptions'
    JOIN "AthleteSeasonStatisticStat" st ON st."AthleteSeasonStatisticCategoryId" = c."Id" AND st."Name" = 'interceptions'
),
cmp AS (
    SELECT m.*,
           coalesce((SELECT sum(st."Value")
                     FROM games g
                     JOIN "AthleteCompetitionStatistic" s ON s."AthleteSeasonId" = g."AthleteSeasonId" AND s."CompetitionId" = g."CompetitionId"
                     JOIN "AthleteCompetitionStatisticCategory" c ON c."AthleteCompetitionStatisticId" = s."Id" AND c."Name" = 'defensiveInterceptions'
                     JOIN "AthleteCompetitionStatisticStat" st ON st."AthleteCompetitionStatisticCategoryId" = c."Id" AND st."Name" = 'interceptions'
                     WHERE g."AthleteSeasonId" = m."AthleteSeasonId" AND g.finalized < m."CreatedUtc"), 0) AS before_value,
           coalesce((SELECT st."Value"
                     FROM "AthleteCompetitionStatistic" s
                     JOIN "AthleteCompetitionStatisticCategory" c ON c."AthleteCompetitionStatisticId" = s."Id" AND c."Name" = 'defensiveInterceptions'
                     JOIN "AthleteCompetitionStatisticStat" st ON st."AthleteCompetitionStatisticCategoryId" = c."Id" AND st."Name" = 'interceptions'
                     WHERE s."AthleteSeasonId" = m."AthleteSeasonId" AND s."CompetitionId" = m.live_competition LIMIT 1), 0) AS live_game_value
    FROM mid m
)
SELECT count(*) AS mid_game_docs_with_ints_stat,
       count(*) FILTER (WHERE season_value = before_value) AS excludes_live_game,
       count(*) FILTER (WHERE season_value > before_value) AS includes_live_game,
       count(*) FILTER (WHERE season_value < before_value) AS behind,
       count(*) FILTER (WHERE live_game_value > 0) AS live_game_had_an_int,
       count(*) FILTER (WHERE live_game_value > 0 AND season_value > before_value) AS live_int_already_in_season_doc
FROM cmp;
