-- Task 01 (data spike) — AthleteCompetitionStatistic coverage for finalized 2026 games.
-- Database: sdProducer.FootballNcaa (local).

-- A. Per finalized 2026 contest: does it have per-game athlete stats, and how long after the final were they first written?
WITH finals AS (
    SELECT ct."Id" AS contest_id, co."Id" AS competition_id, ct."FinalizedUtc"
    FROM "Contest" ct JOIN "Competition" co ON co."ContestId" = ct."Id"
    WHERE ct."SeasonYear" = 2026 AND ct."FinalizedUtc" IS NOT NULL
),
per_game AS (
    SELECT f.contest_id, f."FinalizedUtc",
           (SELECT count(*) FROM "AthleteCompetitionStatistic" s WHERE s."CompetitionId" = f.competition_id) AS stat_docs,
           (SELECT min(s."CreatedUtc") FROM "AthleteCompetitionStatistic" s WHERE s."CompetitionId" = f.competition_id) AS first_written,
           (SELECT count(DISTINCT pp."AthleteSeasonId") FROM "CompetitionPlay" p
              JOIN "CompetitionPlayParticipant" pp ON pp."CompetitionPlayId" = p."Id"
             WHERE p."CompetitionId" = f.competition_id) AS play_athletes
    FROM finals f
)
SELECT count(*) AS finalized_contests,
       count(*) FILTER (WHERE stat_docs > 0) AS with_game_stats,
       count(*) FILTER (WHERE stat_docs = 0 AND play_athletes > 0) AS plays_but_no_game_stats,
       percentile_cont(0.5) WITHIN GROUP (ORDER BY extract(epoch FROM first_written - "FinalizedUtc") / 60)
           FILTER (WHERE stat_docs > 0) AS median_minutes_final_to_first_write,
       percentile_cont(0.9) WITHIN GROUP (ORDER BY extract(epoch FROM first_written - "FinalizedUtc") / 60)
           FILTER (WHERE stat_docs > 0) AS p90_minutes_final_to_first_write
FROM per_game;

-- B. Athletes who appear in a finalized game's plays but have no per-game stat document for it.
WITH finals AS (
    SELECT co."Id" AS competition_id
    FROM "Contest" ct JOIN "Competition" co ON co."ContestId" = ct."Id"
    WHERE ct."SeasonYear" = 2026 AND ct."FinalizedUtc" IS NOT NULL
),
play_athletes AS (
    SELECT DISTINCT p."CompetitionId", pp."AthleteSeasonId"
    FROM finals f
    JOIN "CompetitionPlay" p ON p."CompetitionId" = f.competition_id
    JOIN "CompetitionPlayParticipant" pp ON pp."CompetitionPlayId" = p."Id"
    WHERE pp."AthleteSeasonId" IS NOT NULL
)
SELECT count(*) AS athlete_games_in_plays,
       count(*) FILTER (WHERE EXISTS (SELECT 1 FROM "AthleteCompetitionStatistic" s
                                      WHERE s."CompetitionId" = pa."CompetitionId" AND s."AthleteSeasonId" = pa."AthleteSeasonId")) AS with_game_stat_doc
FROM play_athletes pa;
