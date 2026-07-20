-- Point-in-time records coverage check, by season (football).
-- See docs/features/point-in-time-team-records.md.
--
-- For a COMPLETED season (e.g. 2025) every team competition should have exactly
-- 2 CompetitionCompetitor rows, each with a 'total' AND a 'vsconf'
-- CompetitionCompetitorRecord. Any row returned by the DETAIL query is a gap.
--
-- For the CURRENT season, un-played games legitimately have no records yet
-- (ESPN's competitor record is post-game), so restrict to finalized contests
-- before trusting the numbers (see the commented FinalizedUtc filter).

-- ── change the season here ──────────────────────────────────────────────────
-- (psql) \set season 2025

WITH football_comp AS (
    SELECT co."Id" AS competition_id,
           ct."Id" AS contest_id
    FROM public."Contest" ct
    JOIN public."Competition" co ON co."ContestId" = ct."Id"
    WHERE ct."SeasonYear" = 2025
    -- current-season only: AND ct."FinalizedUtc" IS NOT NULL
),
cc AS (
    SELECT c."Id",
           c."CompetitionId",
           EXISTS (SELECT 1 FROM public."CompetitionCompetitorRecord" r
                   WHERE r."CompetitionCompetitorId" = c."Id" AND r."Type" = 'total')  AS has_total,
           EXISTS (SELECT 1 FROM public."CompetitionCompetitorRecord" r
                   WHERE r."CompetitionCompetitorId" = c."Id" AND r."Type" = 'vsconf') AS has_vsconf
    FROM public."CompetitionCompetitor" c
    WHERE c."Discriminator" = 'FootballCompetitionCompetitor'
      AND c."CompetitionId" IN (SELECT competition_id FROM football_comp)
),
per_comp AS (
    SELECT fc.competition_id,
           fc.contest_id,
           COUNT(cc."Id")                                 AS competitor_count,
           COUNT(cc."Id") FILTER (WHERE cc.has_total)     AS with_total,
           COUNT(cc."Id") FILTER (WHERE cc.has_vsconf)    AS with_vsconf
    FROM football_comp fc
    LEFT JOIN cc ON cc."CompetitionId" = fc.competition_id
    GROUP BY fc.competition_id, fc.contest_id
)

-- ── SUMMARY ─────────────────────────────────────────────────────────────────
SELECT
    COUNT(*)                                                                              AS competitions,
    COUNT(*) FILTER (WHERE competitor_count = 2 AND with_total = 2 AND with_vsconf = 2)    AS fully_covered,
    COUNT(*) FILTER (WHERE competitor_count <> 2)                                          AS missing_competitors,
    COUNT(*) FILTER (WHERE competitor_count = 2 AND (with_total <> 2 OR with_vsconf <> 2)) AS missing_records
FROM per_comp;

-- ── DETAIL (uncomment to list the gaps) ─────────────────────────────────────
-- SELECT contest_id, competition_id, competitor_count, with_total, with_vsconf
-- FROM per_comp
-- WHERE competitor_count <> 2 OR with_total <> 2 OR with_vsconf <> 2
-- ORDER BY contest_id;

-- ========= RESULTS ==================
---------------------------------------
-- 2024
-- competitions	fully_covered	missing_competitors	missing_records
-- 3802	3737	0	65
---------------------------------------
-- 2025
-- competitions	fully_covered	missing_competitors	missing_records
-- 3833	5	0	3828
