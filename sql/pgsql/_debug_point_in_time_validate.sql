-- Validate the entering-record lag used in GetMatchupsByContestIds.sql.
-- For each of LSU 2024's games (by date), show this game's POST-game record and
-- the ENTERING record derived by lagging to the most-recent prior game with a
-- 'total' record. Expected: entering(row N) == post-game(row N-1), and the
-- schedule column (e.g. Alabama Nov 9: entering 6-2 (3-1), post 6-3 (3-2)); the
-- opener has a NULL entering (→ 0 via COALESCE in the real query).
-- LSU 2024 FranchiseSeason = 8f90c51b-d906-2c02-e8e8-2ac0ac6340ae

WITH games AS (
    SELECT cc."Id" AS cc_id, ct."StartDateUtc"
    FROM public."CompetitionCompetitor" cc
    JOIN public."Competition" comp ON comp."Id" = cc."CompetitionId"
    JOIN public."Contest" ct ON ct."Id" = comp."ContestId"
    WHERE cc."FranchiseSeasonId" = '8f90c51b-d906-2c02-e8e8-2ac0ac6340ae'
)
SELECT
    g."StartDateUtc",
    (SELECT r."Summary" FROM public."CompetitionCompetitorRecord" r
     WHERE r."CompetitionCompetitorId" = g.cc_id AND r."Type" = 'total')  AS post_total,
    (SELECT r."Summary" FROM public."CompetitionCompetitorRecord" r
     WHERE r."CompetitionCompetitorId" = g.cc_id AND r."Type" = 'vsconf') AS post_conf,
    enter."Wins" || '-' || enter."Losses"                       AS entering_total,
    enter."ConferenceWins" || '-' || enter."ConferenceLosses"   AS entering_conf
FROM games g
LEFT JOIN LATERAL (
    SELECT
        split_part(tot."Summary", '-', 1)::int  AS "Wins",
        split_part(tot."Summary", '-', 2)::int  AS "Losses",
        split_part(conf."Summary", '-', 1)::int AS "ConferenceWins",
        split_part(conf."Summary", '-', 2)::int AS "ConferenceLosses"
    FROM public."CompetitionCompetitor" prev_cc
    JOIN public."Competition" prev_comp ON prev_comp."Id" = prev_cc."CompetitionId"
    JOIN public."Contest" prev_ct ON prev_ct."Id" = prev_comp."ContestId"
    JOIN public."CompetitionCompetitorRecord" tot
        ON tot."CompetitionCompetitorId" = prev_cc."Id" AND tot."Type" = 'total'
    LEFT JOIN public."CompetitionCompetitorRecord" conf
        ON conf."CompetitionCompetitorId" = prev_cc."Id" AND conf."Type" = 'vsconf'
    WHERE prev_cc."FranchiseSeasonId" = '8f90c51b-d906-2c02-e8e8-2ac0ac6340ae'
      AND prev_ct."StartDateUtc" < g."StartDateUtc"
    ORDER BY prev_ct."StartDateUtc" DESC
    LIMIT 1
) enter ON TRUE
ORDER BY g."StartDateUtc";

-- StartDateUtc	post_total	post_conf	entering_total	entering_conf
-- 2024-09-01 23:30:00+00	0-1	0-0	NULL	NULL
-- 2024-09-07 23:30:00+00	1-1	0-0	0-1	0-0
-- 2024-09-14 16:00:00+00	2-1	1-0	1-1	0-0
-- 2024-09-21 19:30:00+00	3-1	1-0	2-1	1-0
-- 2024-09-28 23:45:00+00	4-1	1-0	3-1	1-0
-- 2024-10-12 23:30:00+00	5-1	2-0	4-1	1-0
-- 2024-10-19 23:00:00+00	6-1	3-0	5-1	2-0
-- 2024-10-26 23:30:00+00	6-2	3-1	6-1	3-0
-- 2024-11-10 00:30:00+00	6-3	3-2	6-2	3-1
-- 2024-11-16 20:30:00+00	6-4	3-3	6-3	3-2
-- 2024-11-24 00:45:00+00	7-4	4-3	6-4	3-3
-- 2024-12-01 00:00:00+00	8-4	5-3	7-4	4-3
-- 2024-12-31 20:30:00+00	9-4	5-3	8-4	5-3
