-- Task 01 (data spike) — "as of this play" season interception count, derived from play participants.
-- Database: sdProducer.FootballNcaa (local). Participants exist for the whole 2026 season (stored since 2026-07-22).
-- Interception plays that name the defender: 26 PassInterceptionReturn, 36 InterceptionReturnTouchdown (role passDefender).
-- 63 Interception never names the defender (passer only) and cannot be attributed.

-- A. The worked example: Tamarcus Cooley's interception (play bb0fb521-…), counted as of that play.
WITH target AS (
    SELECT p."Id" AS play_id, p."CompetitionId", p."SequenceNumber", ct."StartDateUtc" AS kickoff, pp."AthleteSeasonId"
    FROM "CompetitionPlay" p
    JOIN "Competition" co ON co."Id" = p."CompetitionId"
    JOIN "Contest" ct ON ct."Id" = co."ContestId"
    JOIN "CompetitionPlayParticipant" pp ON pp."CompetitionPlayId" = p."Id" AND pp."Type" = 'passDefender'
    WHERE p."Id" = 'bb0fb521-bf2f-08f5-afb0-97693bed4a07'
)
SELECT t.play_id,
       count(*) FILTER (WHERE ct."StartDateUtc" < t.kickoff)                         AS earlier_games,
       count(*) FILTER (WHERE p."CompetitionId" = t."CompetitionId"
                          AND p."SequenceNumber"::numeric <= t."SequenceNumber"::numeric) AS this_game_so_far,
       count(*) FILTER (WHERE ct."StartDateUtc" < t.kickoff
                           OR (p."CompetitionId" = t."CompetitionId"
                               AND p."SequenceNumber"::numeric <= t."SequenceNumber"::numeric)) AS season_as_of_play
FROM target t
JOIN "CompetitionPlayParticipant" pp ON pp."AthleteSeasonId" = t."AthleteSeasonId" AND pp."Type" = 'passDefender'
JOIN "CompetitionPlay" p ON p."Id" = pp."CompetitionPlayId" AND p."TypeId" IN ('26','36')
JOIN "Competition" co ON co."Id" = p."CompetitionId"
JOIN "Contest" ct ON ct."Id" = co."ContestId" AND ct."SeasonYear" = 2026
GROUP BY t.play_id;

-- B. Agreement at scale: play-derived interceptions (finalized games written before the season doc)
--    vs the newest AthleteSeasonStatistic value, for every 2026 athlete with either > 0.
WITH newest AS (
    SELECT DISTINCT ON (s."AthleteSeasonId") s."AthleteSeasonId", s."CreatedUtc", st."Value" AS season_value
    FROM "AthleteSeasonStatistic" s
    JOIN "AthleteSeasonStatisticCategory" c ON c."AthleteSeasonStatisticId" = s."Id" AND c."Name" = 'defensiveInterceptions'
    JOIN "AthleteSeasonStatisticStat" st ON st."AthleteSeasonStatisticCategoryId" = c."Id" AND st."Name" = 'interceptions'
    WHERE s."SplitId" = '0'
      AND s."AthleteSeasonId" IN (
          SELECT ac."AthleteSeasonId" FROM "AthleteCompetition" ac
          JOIN "Competition" co ON co."Id" = ac."CompetitionId"
          JOIN "Contest" ct ON ct."Id" = co."ContestId" WHERE ct."SeasonYear" = 2026)
    ORDER BY s."AthleteSeasonId", s."CreatedUtc" DESC
),
from_plays AS (
    SELECT pp."AthleteSeasonId", ct."FinalizedUtc"
    FROM "CompetitionPlayParticipant" pp
    JOIN "CompetitionPlay" p ON p."Id" = pp."CompetitionPlayId" AND p."TypeId" IN ('26','36')
    JOIN "Competition" co ON co."Id" = p."CompetitionId"
    JOIN "Contest" ct ON ct."Id" = co."ContestId" AND ct."SeasonYear" = 2026
    WHERE pp."Type" = 'passDefender'
),
cmp AS (
    SELECT n."AthleteSeasonId", n.season_value,
           (SELECT count(*) FROM from_plays f WHERE f."AthleteSeasonId" = n."AthleteSeasonId" AND f."FinalizedUtc" < n."CreatedUtc") AS plays_value
    FROM newest n
    WHERE n."AthleteSeasonId" IN (SELECT "AthleteSeasonId" FROM from_plays)
       OR n.season_value > 0
)
SELECT count(*) AS athletes,
       count(*) FILTER (WHERE plays_value = season_value) AS agree,
       count(*) FILTER (WHERE plays_value < season_value) AS plays_lower,
       count(*) FILTER (WHERE plays_value > season_value) AS plays_higher
FROM cmp
WHERE season_value > 0 OR plays_value > 0;
