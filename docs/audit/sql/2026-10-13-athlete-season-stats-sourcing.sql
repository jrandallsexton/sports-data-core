-- Athlete season statistics: what sources them, and how complete they are (2026-10-13).
-- Database: sdProducer.FootballNcaa or sdProducer.FootballNfl (Producer). Read-only.
-- Section 5 runs against sdProvider.<Sport> instead.
--
-- How AthleteSeasonStatistic gets written in season (traced in code 2026-10-13):
--   No job sources it directly. The only automatic in-season refresh is a side effect of the poll crawl:
--   Provider ResourceIndex cron row "seasons/{year}/rankings" (NCAA: '5 14 * * 0', Sundays 14:05)
--     -> SeasonRanking / SeasonPoll -> SeasonTypeWeekRankings
--     -> TeamSeason for every team in the poll (ranked, receiving votes, dropped out), NEW poll-weeks only
--        (SeasonTypeWeekRankingsDocumentProcessor.cs:139; an existing poll-week spawns nothing)
--     -> roster AthleteSeason docs -> AthleteSeasonStatistics   (AthleteSeasonDocumentProcessor.cs:219)
--     -> TeamSeasonLeaders -> AthleteSeasonStatistics for leaders (TeamSeasonLeadersDocumentProcessor.cs:268)
--   Every write in that chain carries the ResourceIndex row's Id as its CorrelationId (CreatedBy / ModifiedBy).
--
--   FranchiseSeasonEnrichmentJob (Producer, Cron.Weekly) requests TeamSeason for EVERY current-season team,
--   but with IncludeLinkedDocumentTypes = [TeamSeasonStatistics] (FranchiseSeasonEnrichmentJob.cs:172),
--   so it refreshes team season stats only: no roster, no leaders, no athlete stats.
--   Manual only: POST athlete season statistics sourcing (AthleteController.cs:99, RequestAthleteSeasonStatisticsSourcing).
--
-- Consequences:
--   NFL has no polls and no rankings cron row, so its 2026 athlete season stats, team leaders and team records
--   were never written after the 08-15 load. NCAA gets them weekly, but only for teams that appear in a poll.

\set season 2026

\echo == 1. Season-scoped tables: rows and newest write, this season vs last
SELECT 'AthleteSeason' AS t, fs."SeasonYear" AS y, count(*) AS n, max(coalesce(a."ModifiedUtc", a."CreatedUtc")) AS newest
FROM "AthleteSeason" a JOIN "FranchiseSeason" fs ON fs."Id" = a."FranchiseSeasonId"
WHERE fs."SeasonYear" IN (:season - 1, :season) GROUP BY 1, 2
UNION ALL SELECT 'AthleteSeasonStatistic', fs."SeasonYear", count(*), max(s."CreatedUtc")
FROM "AthleteSeasonStatistic" s JOIN "AthleteSeason" a ON a."Id" = s."AthleteSeasonId" JOIN "FranchiseSeason" fs ON fs."Id" = a."FranchiseSeasonId"
WHERE fs."SeasonYear" IN (:season - 1, :season) GROUP BY 1, 2
UNION ALL SELECT 'AthleteCompetitionStatistic (per game)', fs."SeasonYear", count(*), max(s."CreatedUtc")
FROM "AthleteCompetitionStatistic" s JOIN "AthleteSeason" a ON a."Id" = s."AthleteSeasonId" JOIN "FranchiseSeason" fs ON fs."Id" = a."FranchiseSeasonId"
WHERE fs."SeasonYear" IN (:season - 1, :season) GROUP BY 1, 2
UNION ALL SELECT 'FranchiseSeasonStatisticCategory', fs."SeasonYear", count(*), max(coalesce(c."ModifiedUtc", c."CreatedUtc"))
FROM "FranchiseSeasonStatisticCategory" c JOIN "FranchiseSeason" fs ON fs."Id" = c."FranchiseSeasonId"
WHERE fs."SeasonYear" IN (:season - 1, :season) GROUP BY 1, 2
UNION ALL SELECT 'FranchiseSeasonLeader', fs."SeasonYear", count(*), max(l."CreatedUtc")
FROM "FranchiseSeasonLeader" l JOIN "FranchiseSeason" fs ON fs."Id" = l."FranchiseSeasonId"
WHERE fs."SeasonYear" IN (:season - 1, :season) GROUP BY 1, 2
UNION ALL SELECT 'FranchiseSeasonRecord', "SeasonYear", count(*), max(coalesce("ModifiedUtc", "CreatedUtc"))
FROM "FranchiseSeasonRecord" WHERE "SeasonYear" IN (:season - 1, :season) GROUP BY 1, 2
UNION ALL SELECT 'SeasonTypeLeader', "SeasonYear", count(*), max("CreatedUtc")
FROM "SeasonTypeLeader" WHERE "SeasonYear" IN (:season - 1, :season) GROUP BY 1, 2
UNION ALL SELECT 'Contest finalized', "SeasonYear", count(*) FILTER (WHERE "FinalizedUtc" IS NOT NULL), max("FinalizedUtc")
FROM "Contest" WHERE "SeasonYear" IN (:season - 1, :season) GROUP BY 1, 2
ORDER BY 1, 2;

\echo == 2. Write timeline this season: athlete stats, team leaders, roster rows by day (after the summer load)
SELECT day, sum(stats) AS athlete_season_stats, sum(leaders) AS team_leaders, sum(roster) AS roster_rows_modified
FROM (
  SELECT s."CreatedUtc"::date AS day, 1 AS stats, 0 AS leaders, 0 AS roster
  FROM "AthleteSeasonStatistic" s JOIN "AthleteSeason" a ON a."Id" = s."AthleteSeasonId" JOIN "FranchiseSeason" fs ON fs."Id" = a."FranchiseSeasonId"
  WHERE fs."SeasonYear" = :season
  UNION ALL SELECT l."CreatedUtc"::date, 0, 1, 0
  FROM "FranchiseSeasonLeader" l JOIN "FranchiseSeason" fs ON fs."Id" = l."FranchiseSeasonId" WHERE fs."SeasonYear" = :season
  UNION ALL SELECT a."ModifiedUtc"::date, 0, 0, 1
  FROM "AthleteSeason" a JOIN "FranchiseSeason" fs ON fs."Id" = a."FranchiseSeasonId"
  WHERE fs."SeasonYear" = :season AND a."ModifiedUtc" IS NOT NULL) x
GROUP BY day ORDER BY day;

\echo == 3. Which correlation wrote the athlete stats (match the big one to a ResourceIndex Id in section 5)
-- Stats are delete-and-insert on refresh, so CreatedUtc/CreatedBy reflect the latest write.
SELECT s."CreatedBy" AS correlation_id, min(s."CreatedUtc") AS first_write, max(s."CreatedUtc") AS last_write, count(*) AS rows
FROM "AthleteSeasonStatistic" s JOIN "AthleteSeason" a ON a."Id" = s."AthleteSeasonId" JOIN "FranchiseSeason" fs ON fs."Id" = a."FranchiseSeasonId"
WHERE fs."SeasonYear" = :season
GROUP BY 1 HAVING count(*) >= 50 ORDER BY rows DESC;

\echo == 4a. Coverage by team: teams with a finalized game this season vs teams with any athlete season stats
WITH played AS (
  SELECT DISTINCT fs."Id" FROM "FranchiseSeason" fs
  JOIN "Contest" k ON fs."Id" IN (k."HomeTeamFranchiseSeasonId", k."AwayTeamFranchiseSeasonId")
  WHERE fs."SeasonYear" = :season AND k."FinalizedUtc" IS NOT NULL),
with_stats AS (
  SELECT DISTINCT a."FranchiseSeasonId" AS id FROM "AthleteSeasonStatistic" s JOIN "AthleteSeason" a ON a."Id" = s."AthleteSeasonId")
SELECT count(*) AS teams_played, count(*) FILTER (WHERE "Id" IN (SELECT id FROM with_stats)) AS teams_with_athlete_stats
FROM played;

\echo == 4b. Coverage by athlete: athletes in a play this season vs athletes with a season-stat row, and how stale
WITH inplay AS (
  SELECT pp."AthleteSeasonId" AS id, max(k."StartDateUtc") AS last_game
  FROM "CompetitionPlayParticipant" pp
  JOIN "CompetitionPlay" p ON p."Id" = pp."CompetitionPlayId"
  JOIN "Competition" c ON c."Id" = p."CompetitionId"
  JOIN "Contest" k ON k."Id" = c."ContestId"
  WHERE k."SeasonYear" = :season AND pp."AthleteSeasonId" IS NOT NULL
  GROUP BY 1)
SELECT count(*) AS athletes_in_plays,
       count(*) FILTER (WHERE st.created IS NOT NULL) AS with_season_stats,
       count(*) FILTER (WHERE st.created IS NOT NULL AND st.created < i.last_game) AS stats_older_than_last_game
FROM inplay i
LEFT JOIN LATERAL (SELECT max(s."CreatedUtc") AS created FROM "AthleteSeasonStatistic" s
                   WHERE s."AthleteSeasonId" = i.id AND s."SplitId" = '0') st ON true;

\echo == 4c. Athletes in plays this season whose roster row has no team (no roster refresh or sourcing command reaches them)
-- 2026-10-12 restore: NFL 96 athletes / 895 participations; NCAA (10-10 restore) 2,471 / 24,472.
SELECT (a."FranchiseSeasonId" IS NULL) AS no_team, count(DISTINCT pp."AthleteSeasonId") AS athletes, count(*) AS participations
FROM "CompetitionPlayParticipant" pp
JOIN "CompetitionPlay" p ON p."Id" = pp."CompetitionPlayId"
JOIN "Competition" c ON c."Id" = p."CompetitionId"
JOIN "Contest" k ON k."Id" = c."ContestId"
JOIN "AthleteSeason" a ON a."Id" = pp."AthleteSeasonId"
WHERE k."SeasonYear" = :season
GROUP BY 1 ORDER BY 1;

-- 5. Run against sdProvider.<Sport>: the recurring crawls that exist. NCAA's rankings row Id is the correlation in section 3;
--    NFL has no recurring rows at all (2026-10-13).
-- SELECT "Id", "Ordinal", "Name", "CronExpression", "CronTimeZoneId", "IsEnabled", "SeasonYear", "LastCompletedUtc", "Uri"
-- FROM "ResourceIndex" WHERE "IsRecurring" ORDER BY "Ordinal";
