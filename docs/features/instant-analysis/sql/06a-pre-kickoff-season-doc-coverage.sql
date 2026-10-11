-- Task 06: how often a lab replay can use a season document written before the game's kickoff.
-- Database: sdProducer.FootballNcaa (local, prod restore).
-- AthleteSeasonStatistic keeps one row per ESPN ref; each refresh deletes and re-inserts it,
-- so CreatedUtc is the time of the LAST rewrite and there is no history to pick an older version from.

-- A. Rows per athlete-season (split 0), 2026 athletes.
SELECT n_docs, count(*) AS athlete_seasons
FROM (
    SELECT s."AthleteSeasonId", count(*) AS n_docs
    FROM "AthleteSeasonStatistic" s
    JOIN "AthleteSeason" a ON a."Id" = s."AthleteSeasonId"
    JOIN "FranchiseSeason" fs ON fs."Id" = a."FranchiseSeasonId" AND fs."SeasonYear" = 2026
    WHERE s."SplitId" = '0'
    GROUP BY 1) x
GROUP BY 1 ORDER BY 1;

-- B. For participants in finalized 2026 games: does a split-0 document exist that was written before kickoff?
WITH ap AS (
    SELECT DISTINCT p."CompetitionId", pp."AthleteSeasonId", ct."StartDateUtc" AS kickoff
    FROM "CompetitionPlayParticipant" pp
    JOIN "CompetitionPlay" p ON p."Id" = pp."CompetitionPlayId"
    JOIN "Competition" co ON co."Id" = p."CompetitionId"
    JOIN "Contest" ct ON ct."Id" = co."ContestId" AND ct."SeasonYear" = 2026 AND ct."FinalizedUtc" IS NOT NULL
    WHERE pp."AthleteSeasonId" IS NOT NULL)
SELECT count(*) AS athlete_games,
       count(*) FILTER (WHERE EXISTS (SELECT 1 FROM "AthleteSeasonStatistic" s
                                      WHERE s."AthleteSeasonId" = ap."AthleteSeasonId" AND s."SplitId" = '0'
                                        AND s."CreatedUtc" < ap.kickoff)) AS pre_kickoff_doc,
       count(*) FILTER (WHERE EXISTS (SELECT 1 FROM "AthleteSeasonStatistic" s
                                      WHERE s."AthleteSeasonId" = ap."AthleteSeasonId" AND s."SplitId" = '0')) AS any_doc
FROM ap;
