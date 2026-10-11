-- Task 01 (data spike) — is the newest AthleteSeasonStatistic written BEFORE kickoff current?
-- For each 2026 athlete-game (2nd game onward): was a season document written between the previous game's
-- finalization and this game's kickoff (i.e. does the pre-kickoff document already reflect the previous game)?
-- Database: sdProducer.FootballNcaa (local).
WITH games AS (
    SELECT ac."AthleteSeasonId", ct."StartDateUtc" AS kickoff, ct."FinalizedUtc" AS finalized,
           lag(ct."FinalizedUtc") OVER (PARTITION BY ac."AthleteSeasonId" ORDER BY ct."StartDateUtc") AS prev_finalized
    FROM "AthleteCompetition" ac
    JOIN "Competition" co ON co."Id" = ac."CompetitionId"
    JOIN "Contest" ct ON ct."Id" = co."ContestId"
    WHERE ct."SeasonYear" = 2026 AND ct."FinalizedUtc" IS NOT NULL
),
athletes_with_season_docs AS (
    SELECT DISTINCT "AthleteSeasonId" FROM "AthleteSeasonStatistic"
)
SELECT count(*) AS athlete_games,
       count(*) FILTER (WHERE EXISTS (SELECT 1 FROM "AthleteSeasonStatistic" s
                                      WHERE s."AthleteSeasonId" = g."AthleteSeasonId"
                                        AND s."CreatedUtc" > g.prev_finalized AND s."CreatedUtc" < g.kickoff)) AS fresh_before_kickoff,
       count(*) FILTER (WHERE NOT EXISTS (SELECT 1 FROM "AthleteSeasonStatistic" s
                                          WHERE s."AthleteSeasonId" = g."AthleteSeasonId" AND s."CreatedUtc" < g.kickoff)) AS no_doc_before_kickoff
FROM games g
JOIN athletes_with_season_docs a ON a."AthleteSeasonId" = g."AthleteSeasonId"
WHERE g.prev_finalized IS NOT NULL;
