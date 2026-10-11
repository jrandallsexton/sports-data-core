-- Task 07: freshness guard and name coverage. Database: sdProducer.FootballNcaa (local, prod restore).

-- A. Names available for templates (2026 athlete-seasons that appear in plays).
SELECT count(DISTINCT a."Id") AS athletes,
       count(DISTINCT a."Id") FILTER (WHERE coalesce(a."ShortName", '') <> '') AS with_short_name,
       count(DISTINCT a."Id") FILTER (WHERE coalesce(a."DisplayName", '') <> '') AS with_display_name
FROM "CompetitionPlayParticipant" pp
JOIN "AthleteSeason" a ON a."Id" = pp."AthleteSeasonId"
JOIN "FranchiseSeason" fs ON fs."Id" = a."FranchiseSeasonId" AND fs."SeasonYear" = 2026;

SELECT a."ShortName", a."DisplayName" FROM "AthleteSeason" a
WHERE a."Id" IN (SELECT "AthleteSeasonId" FROM "CompetitionPlayParticipant" WHERE "CompetitionPlayId" = 'bb0fb521-bf2f-08f5-afb0-97693bed4a07');

-- B. Freshness: for each finalized 2026 game, take each participant's team's PREVIOUS game; is the athlete's
--    season document as of now written after that previous game was finalized? (Today's documents only; the
--    true at-kickoff state is not recoverable, see task 06.) Measured on each team's latest game, where "now"
--    is closest to what live would have seen.
WITH latest AS (
    SELECT DISTINCT ON (t.fs) t.fs, ct."Id" AS contest_id, ct."StartDateUtc" AS kickoff
    FROM "Contest" ct
    CROSS JOIN LATERAL (VALUES (ct."HomeTeamFranchiseSeasonId"), (ct."AwayTeamFranchiseSeasonId")) t(fs)
    WHERE ct."SeasonYear" = 2026 AND ct."FinalizedUtc" IS NOT NULL
    ORDER BY t.fs, ct."StartDateUtc" DESC),
prev AS (
    SELECT l.fs, l.contest_id, l.kickoff,
           (SELECT p."FinalizedUtc" FROM "Contest" p
            WHERE p."SeasonYear" = 2026 AND p."StartDateUtc" < l.kickoff
              AND l.fs IN (p."HomeTeamFranchiseSeasonId", p."AwayTeamFranchiseSeasonId")
            ORDER BY p."StartDateUtc" DESC LIMIT 1) AS prev_final
    FROM latest l),
ath AS (
    SELECT DISTINCT pv.fs, pv.prev_final, pp."AthleteSeasonId"
    FROM prev pv
    JOIN "Competition" co ON co."ContestId" = pv.contest_id
    JOIN "CompetitionPlay" p ON p."CompetitionId" = co."Id"
    JOIN "CompetitionPlayParticipant" pp ON pp."CompetitionPlayId" = p."Id"
    JOIN "AthleteSeason" a ON a."Id" = pp."AthleteSeasonId" AND a."FranchiseSeasonId" = pv.fs
    WHERE pv.prev_final IS NOT NULL)
SELECT count(*) AS athletes,
       count(*) FILTER (WHERE d.created IS NULL) AS no_doc,
       count(*) FILTER (WHERE d.created >= a.prev_final) AS fresh,
       count(*) FILTER (WHERE d.created < a.prev_final) AS stale
FROM ath a
LEFT JOIN LATERAL (SELECT max(s."CreatedUtc") AS created FROM "AthleteSeasonStatistic" s
                   WHERE s."AthleteSeasonId" = a."AthleteSeasonId" AND s."SplitId" = '0') d ON true;
