-- War Room analytics — Phase 0b coverage audit (NCAA football, 2000-present)
-- Database: sdProducer.FootballNcaa. Read-only.
-- Findings: docs/features/war-room-analytics/2026-11-01-coverage-audit.md
--
-- Population: non-cancelled contests with at least one FBS participant
-- (FranchiseSeason.GroupSeasonMap ILIKE '%|fbs%', the same FBS predicate
-- production uses). One row per season, one column per data family.

-- ---------------------------------------------------------------------------
-- 1. Per-season coverage by data family
-- ---------------------------------------------------------------------------
WITH fbs AS (
    SELECT "Id", "GroupSeasonMap"
    FROM "FranchiseSeason"
    WHERE "GroupSeasonMap" ILIKE '%|fbs%'
),
g AS (
    SELECT
        c."Id"                          AS contest_id,
        c."SeasonYear"                  AS season,
        c."StartDateUtc",
        c."FinalizedUtc",
        c."HomeScore",
        c."AwayScore",
        c."SpreadWinnerFranchiseSeasonId",
        k."Id"                          AS competition_id,
        k."TimeValid",
        k."IsConferenceCompetition",
        COALESCE(k."VenueId", c."VenueId") AS venue_id,
        hf."Id" IS NOT NULL AND af."Id" IS NOT NULL AS both_fbs,
        -- conference = last segment of the map (NCAAF|NCAA|fbs|sec)
        hf."Id" IS NOT NULL AND af."Id" IS NOT NULL
            AND split_part(hf."GroupSeasonMap", '|', 4) <> ''
            AND split_part(hf."GroupSeasonMap", '|', 4) = split_part(af."GroupSeasonMap", '|', 4)
                                        AS same_conf_by_map
    FROM "Contest" c
    JOIN "Competition" k ON k."ContestId" = c."Id"
    LEFT JOIN fbs hf ON hf."Id" = c."HomeTeamFranchiseSeasonId"
    LEFT JOIN fbs af ON af."Id" = c."AwayTeamFranchiseSeasonId"
    WHERE c."SeasonYear" BETWEEN 2000 AND 2026
      AND c."CancelledUtc" IS NULL
      AND (hf."Id" IS NOT NULL OR af."Id" IS NOT NULL)
)
SELECT
    g.season,
    count(*)                                                        AS games,
    count(*) FILTER (WHERE both_fbs)                                AS both_fbs,
    -- results
    round(100.0 * count(*) FILTER (WHERE "FinalizedUtc" IS NOT NULL
        AND "HomeScore" IS NOT NULL AND "AwayScore" IS NOT NULL) / count(*), 1) AS pct_final_scored,
    -- kickoff time
    round(100.0 * count(*) FILTER (WHERE "TimeValid") / count(*), 1)            AS pct_time_valid,
    round(100.0 * count(*) FILTER (WHERE ("StartDateUtc" AT TIME ZONE 'America/New_York')::time = '00:00') / count(*), 1)
                                                                    AS pct_midnight_et,
    -- venue (needed for local time)
    round(100.0 * count(*) FILTER (WHERE v."Id" IS NOT NULL
        AND (v."Latitude" <> 0 OR v."Longitude" <> 0)) / count(*), 1)            AS pct_venue_coords,
    -- conference flag vs map-derived same conference (both-FBS games only)
    round(100.0 * count(*) FILTER (WHERE both_fbs AND "IsConferenceCompetition")
        / NULLIF(count(*) FILTER (WHERE both_fbs), 0), 1)           AS pct_conf_flag,
    round(100.0 * count(*) FILTER (WHERE both_fbs AND same_conf_by_map)
        / NULLIF(count(*) FILTER (WHERE both_fbs), 0), 1)           AS pct_conf_map,
    round(100.0 * count(*) FILTER (WHERE both_fbs AND "IsConferenceCompetition" = same_conf_by_map)
        / NULLIF(count(*) FILTER (WHERE both_fbs), 0), 1)           AS pct_conf_agree,
    -- rank at game time (ESPN curatedRank on the competitor; 99 = unranked)
    (SELECT count(*) FROM "CompetitionCompetitor" cc
      WHERE cc."CompetitionId" IN (SELECT competition_id FROM g g2 WHERE g2.season = g.season)
        AND cc."CuratedRankCurrent" BETWEEN 1 AND 25)               AS ranked_appearances,
    -- market
    round(100.0 * count(*) FILTER (WHERE EXISTS (SELECT 1 FROM "CompetitionOdds" o
        WHERE o."CompetitionId" = g.competition_id AND o."Spread" IS NOT NULL)) / count(*), 1) AS pct_spread,
    round(100.0 * count(*) FILTER (WHERE "SpreadWinnerFranchiseSeasonId" IS NOT NULL) / count(*), 1) AS pct_ats_result,
    -- box scores and plays
    round(100.0 * count(*) FILTER (WHERE (SELECT count(*) FROM "CompetitionCompetitorStatistics" s
        WHERE s."CompetitionId" = g.competition_id) >= 2) / count(*), 1)        AS pct_team_box,
    round(100.0 * count(*) FILTER (WHERE EXISTS (SELECT 1 FROM "AthleteCompetitionStatistic" a
        WHERE a."CompetitionId" = g.competition_id)) / count(*), 1)            AS pct_athlete_box,
    round(100.0 * count(*) FILTER (WHERE EXISTS (SELECT 1 FROM "CompetitionPlay" p
        WHERE p."CompetitionId" = g.competition_id)) / count(*), 1)            AS pct_plays
FROM g
LEFT JOIN "Venue" v ON v."Id" = g.venue_id
GROUP BY g.season
ORDER BY g.season;

-- ---------------------------------------------------------------------------
-- 2. Weekly poll history. Two stores: FranchiseSeasonRanking (team-season
--    side) and SeasonPoll/SeasonPollWeek/SeasonPollWeekEntry (poll side).
--    A usable weekly AP history has ~15-20 weeks x 25 teams per season.
-- ---------------------------------------------------------------------------
SELECT r."SeasonYear", count(*) AS ap_rows,
       count(DISTINCT r."SeasonWeekId") AS weeks,
       count(DISTINCT r."Date")         AS dates
FROM "FranchiseSeasonRanking" r
WHERE r."Type" = 'ap'
GROUP BY 1 ORDER BY 1;

SELECT p."Slug", p."SeasonYear", count(DISTINCT w."Id") AS weeks, count(e."Id") AS entries
FROM "SeasonPoll" p
LEFT JOIN "SeasonPollWeek" w ON w."SeasonPollId" = p."Id"
LEFT JOIN "SeasonPollWeekEntry" e ON e."SeasonPollWeekId" = w."Id"
GROUP BY 1, 2 ORDER BY 1, 2;

-- ---------------------------------------------------------------------------
-- 3. curatedRank per week (frozen-at-ingestion check; see findings).
--    Healthy: ~17-25 ranked rows per regular-season week (dips = ranked
--    teams on bye; rows > distinct ranks = poll ties). Do NOT judge by the
--    share of 99s: ~95% of competitors (FCS/D2/D3/NAIA) are never ranked.
--    Change the year to compare (2025 is a settled baseline).
-- ---------------------------------------------------------------------------
SELECT date_trunc('week', c."StartDateUtc")::date AS wk,
       count(*) FILTER (WHERE cc."CuratedRankCurrent" BETWEEN 1 AND 25) AS ranked,
       count(DISTINCT cc."CuratedRankCurrent")
           FILTER (WHERE cc."CuratedRankCurrent" BETWEEN 1 AND 25) AS distinct_ranks,
       count(*) FILTER (WHERE c."FinalizedUtc" IS NOT NULL) AS finalized_rows,
       count(*) AS competitor_rows
FROM "Contest" c
JOIN "Competition" k ON k."ContestId" = c."Id"
JOIN "CompetitionCompetitor" cc ON cc."CompetitionId" = k."Id"
WHERE c."SeasonYear" = 2026
GROUP BY 1 ORDER BY 1;

-- ---------------------------------------------------------------------------
-- 4. FBS membership per season (sanity check on the population predicate).
-- ---------------------------------------------------------------------------
SELECT "SeasonYear",
       count(*) FILTER (WHERE "GroupSeasonMap" ILIKE '%|fbs%') AS fbs_teams,
       count(*) AS all_teams
FROM "FranchiseSeason"
WHERE "SeasonYear" BETWEEN 2000 AND 2026
GROUP BY 1 ORDER BY 1;

-- ---------------------------------------------------------------------------
-- 5. Team record drift: FranchiseSeason Wins+Losses+Ties vs finalized games.
-- Historical seasons are only re-enriched when one of their contests
-- finalizes (ContestFinalizedHandler); the weekly FranchiseSeasonEnrichmentJob
-- covers the current season only. Expected: wl_mismatch = 0 everywhere.
--   never_enriched: W+L+T is 0 despite finals, record never computed. (Not
--                   ModifiedUtc IS NULL: membership corrections stamp it too.)
--   wlt_under:      finalized games added after the last enrichment.
--   wlt_over:       a contest lost FinalizedUtc after the last enrichment.
-- Fix: POST api/ops/producer/football/ncaa/franchise-seasons/seasonYear/{year}/enrich
-- (records only, local data, no ESPN), one season at a time. All 26 seasons
-- were 0 on 2026-11-05 after the backfill (4,163 mismatched before).
-- Preseason excluded, matching EnrichFranchiseSeasonHandler.
-- ---------------------------------------------------------------------------
WITH t AS (
    SELECT fs."SeasonYear",
           coalesce(fs."Wins", 0) + coalesce(fs."Losses", 0) + coalesce(fs."Ties", 0) AS wlt,
           (SELECT count(*) FROM "Contest" c
             WHERE c."FinalizedUtc" IS NOT NULL
               AND (c."HomeTeamFranchiseSeasonId" = fs."Id" OR c."AwayTeamFranchiseSeasonId" = fs."Id")
               AND NOT EXISTS (SELECT 1 FROM "SeasonPhase" sp
                                WHERE sp."Id" = c."SeasonPhaseId" AND sp."TypeCode" = 1)) AS n_final
    FROM "FranchiseSeason" fs
    WHERE fs."SeasonYear" BETWEEN 2000 AND 2026
)
SELECT "SeasonYear",
       count(*) FILTER (WHERE n_final > 0)                                  AS teams_with_finals,
       count(*) FILTER (WHERE n_final > 0 AND wlt <> n_final)               AS wl_mismatch,
       count(*) FILTER (WHERE n_final > 0 AND wlt = 0)                     AS never_enriched,
       count(*) FILTER (WHERE n_final > 0 AND wlt < n_final)                AS wlt_under,
       count(*) FILTER (WHERE n_final > 0 AND wlt > n_final)                AS wlt_over
FROM t
GROUP BY 1 ORDER BY 1;

  SELECT c."Id", c."ShortName", c."FinalizedUtc",
         hs."Abbreviation" AS contest_home, aws."Abbreviation" AS contest_away,
         cc."HomeAway", fs."Abbreviation" AS competitor, cc."Order", cc."Winner"
  FROM "Contest" c
  JOIN "Competition" k ON k."ContestId" = c."Id"
  JOIN "CompetitionCompetitor" cc ON cc."CompetitionId" = k."Id"
  JOIN "FranchiseSeason" fs ON fs."Id" = cc."FranchiseSeasonId"
  JOIN "FranchiseSeason" hs ON hs."Id" = c."HomeTeamFranchiseSeasonId"
  JOIN "FranchiseSeason" aws ON aws."Id" = c."AwayTeamFranchiseSeasonId"
  WHERE c."Id" IN (
    'ee5eea3e-5ef7-9624-f80d-f30e5f0117ca','544d9c21-2a61-1afa-4b86-39cee25b04e4',
    '9a8515fb-d9ea-397c-1820-e612c004cfc1','d2628798-7870-1050-5fe0-8f0ccd9f0578',
    '97db8a95-8345-bd4d-10ed-9ec309e1df78','40d6678c-b636-0c78-6cf9-6f2fe0ef75f4',
    '9457edc1-ff8b-0eee-d654-86b81d275cbd','4ce0e9d9-04a9-0fcd-17e8-248020cd0238',
    '96682bb5-134b-1a3b-9979-68e2049d1e63')
  ORDER BY c."Id", cc."HomeAway";

-- season	games	both_fbs	pct_final_scored	pct_time_valid	pct_midnight_et	pct_venue_coords	pct_conf_flag	pct_conf_map	pct_conf_agree	ranked_appearances	pct_spread	pct_ats_result	pct_team_box	pct_athlete_box	pct_plays
-- 2000	711	662	0.0	0.0	99.4	0.0	0.0	37.2	62.8	0	0.0	0.0	0.0	0.0	0.0
-- 2001	753	620	91.6	100.0	0.0	86.1	0.0	51.0	49.0	267	0.0	0.0	0.0	0.0	59.9
-- 2002	762	628	27.4	100.0	0.0	86.5	0.0	51.0	49.0	334	0.0	0.0	0.0	0.0	4.1
-- 2003	759	625	44.4	100.0	0.0	89.5	0.0	51.2	48.8	310	0.0	0.0	0.0	0.0	63.4
-- 2004	700	590	98.6	100.0	0.0	86.4	0.0	55.4	44.6	296	0.0	0.0	97.6	98.0	66.0
-- 2005	688	606	98.3	100.0	0.0	88.5	0.0	62.2	37.8	301	0.0	0.0	97.2	97.2	85.8
-- 2006	748	643	99.6	100.0	0.1	89.8	0.0	58.0	42.0	324	0.0	0.0	99.9	99.9	90.2
-- 2007	754	653	99.7	100.0	0.0	89.4	0.0	57.6	42.4	321	0.0	0.0	98.5	99.2	88.5
-- 2008	765	649	99.7	100.0	0.0	93.6	0.2	58.1	41.8	327	0.1	0.1	99.3	99.5	96.2
-- 2009	766	642	99.9	100.0	0.0	94.3	0.0	60.0	40.0	323	0.0	0.0	99.9	99.9	97.9
-- 2010	205	178	0.0	100.0	0.0	89.3	0.0	57.9	42.1	110	0.0	0.0	100.0	100.0	98.5
-- 2011	780	646	98.7	100.0	0.0	93.6	0.0	66.6	33.4	338	0.0	0.0	38.7	90.0	98.8
-- 2012	819	683	99.8	100.0	0.2	95.0	0.0	67.2	32.8	336	98.8	97.3	98.9	99.0	97.2
-- 2013	858	745	99.8	100.0	0.3	95.7	0.0	66.4	33.6	341	99.0	97.8	99.7	99.7	99.7
-- 2014	873	765	99.7	100.0	0.3	100.0	66.4	67.8	98.6	340	99.7	97.6	99.5	99.7	97.8
-- 2015	873	766	99.7	100.0	0.0	100.0	66.7	67.9	98.8	339	100.0	97.5	99.7	99.7	98.9
-- 2016	751	651	66.2	100.0	0.0	100.0	66.4	67.4	98.9	256	96.3	64.3	36.4	96.1	94.4
-- 2017	856	759	96.4	99.8	0.2	100.0	66.1	67.6	98.6	298	97.4	93.2	95.2	96.5	95.9
-- 2018	895	780	56.2	100.0	0.0	100.0	66.2	68.1	98.1	334	97.5	54.1	9.9	28.0	97.7
-- 2019	889	775	54.7	100.0	0.0	99.0	66.5	68.6	97.8	339	99.6	51.7	99.9	99.9	99.8
-- 2020	691	650	82.3	98.8	1.2	99.3	82.6	84.5	98.2	294	97.7	74.5	82.5	82.5	81.6
-- 2021	893	774	98.9	100.0	0.0	100.0	66.0	68.3	97.7	291	99.4	96.5	99.3	99.3	94.0
-- 2022	900	780	99.6	99.9	0.1	100.0	66.0	68.5	97.6	321	99.8	99.3	99.6	99.6	95.2
-- 2023	910	792	100.0	100.0	0.0	100.0	67.7	69.2	98.5	342	100.0	98.4	100.0	100.0	99.1
-- 2024	920	799	99.8	100.0	0.0	100.0	66.1	68.0	98.1	346	99.1	95.0	99.9	99.9	97.9
-- 2025	934	808	99.5	100.0	0.0	100.0	67.7	69.7	98.0	351	99.5	99.0	95.4	42.3	99.9
-- 2026	846	716	70.4	86.2	13.8	100.0	74.4	73.3	98.9	301	80.0	69.4	57.2	62.6	70.4

-- SeasonYear	ap_rows	weeks	dates
-- 2000	25	0	0
-- 2001	25	0	0
-- 2002	25	0	0
-- 2003	25	0	0
-- 2004	25	0	0
-- 2005	25	0	0
-- 2006	25	0	0
-- 2007	25	0	0
-- 2008	25	0	0
-- 2009	25	0	0
-- 2010	12	0	0
-- 2011	25	0	0
-- 2012	25	0	0
-- 2013	25	0	0
-- 2014	25	0	0
-- 2015	25	0	1
-- 2016	25	0	1
-- 2017	21	0	1
-- 2018	25	0	1
-- 2019	25	0	1
-- 2020	25	0	1
-- 2021	25	0	1
-- 2022	25	0	1
-- 2023	25	0	1
-- 2024	25	0	1
-- 2025	425	15	17
-- 2026	250	9	10

-- Slug	SeasonYear	weeks	entries
-- afca	2025	27	1216
-- afca	2026	22	1070
-- ap	2025	17	705
-- ap	2026	10	406
-- cfp	2025	6	164
-- cfp	2026	1	25
-- fcs	2025	15	665
-- fcs	2026	11	520
-- tournament	2025	1	12
-- usa	2025	17	763
-- usa	2026	10	471

-- wk	ranked	distinct_ranks	finalized_rows	competitor_rows
-- 2026-08-24	1	1	224	228
-- 2026-08-31	24	23	484	486
-- 2026-09-07	26	25	476	476
-- 2026-09-14	25	25	472	472
-- 2026-09-21	24	24	440	440
-- 2026-09-28	19	19	468	468
-- 2026-10-05	21	21	418	422
-- 2026-10-12	22	22	464	464
-- 2026-10-19	19	19	460	460
-- 2026-10-26	20	19	474	474
-- 2026-11-02	23	23	4	496
-- 2026-11-09	26	25	0	468
-- 2026-11-16	26	25	0	252
-- 2026-11-23	25	24	0	134
-- 2026-12-07	0	0	0	2

-- SeasonYear	fbs_teams	all_teams
-- 2000	138	183
-- 2001	118	452
-- 2002	116	488
-- 2003	116	499
-- 2004	116	504
-- 2005	116	343
-- 2006	116	536
-- 2007	117	698
-- 2008	117	710
-- 2009	115	715
-- 2010	115	725
-- 2011	114	739
-- 2012	119	749
-- 2013	135	765
-- 2014	135	773
-- 2015	135	781
-- 2016	128	703
-- 2017	130	780
-- 2018	130	790
-- 2019	130	793
-- 2020	130	786
-- 2021	130	794
-- 2022	131	800
-- 2023	133	814
-- 2024	134	818
-- 2025	136	820
-- 2026	138	828

-- Id	ShortName	FinalizedUtc	contest_home	contest_away	HomeAway	competitor	Order	Winner
-- 40d6678c-b636-0c78-6cf9-6f2fe0ef75f4	CLA VS VAL	2026-11-03 11:45:26.235335+00	VAL	CLA	away	CLA	1	False
-- 40d6678c-b636-0c78-6cf9-6f2fe0ef75f4	CLA VS VAL	2026-11-03 11:45:26.235335+00	VAL	CLA	home	VAL	0	True
-- 4ce0e9d9-04a9-0fcd-17e8-248020cd0238	CEN VS KYST	2026-11-03 11:45:54.040831+00	KYST	CEN	away	CEN	1	False
-- 4ce0e9d9-04a9-0fcd-17e8-248020cd0238	CEN VS KYST	2026-11-03 11:45:54.040831+00	KYST	CEN	home	KYST	0	True
-- 544d9c21-2a61-1afa-4b86-39cee25b04e4	HOW VS MOR	2026-11-03 11:42:50.842654+00	MOR	HOW	away	HOW	1	True
-- 544d9c21-2a61-1afa-4b86-39cee25b04e4	HOW VS MOR	2026-11-03 11:42:50.842654+00	MOR	HOW	home	MOR	0	False
-- 9457edc1-ff8b-0eee-d654-86b81d275cbd	VU VS MIL	2026-11-03 11:45:38.778848+00	MIL	VU	away	VU	1	True
-- 9457edc1-ff8b-0eee-d654-86b81d275cbd	VU VS MIL	2026-11-03 11:45:38.778848+00	MIL	VU	home	MIL	0	False
-- 96682bb5-134b-1a3b-9979-68e2049d1e63	FAMU VS BCU	2026-11-03 11:46:19.889201+00	BCU	FAMU	away	FAMU	1	False
-- 96682bb5-134b-1a3b-9979-68e2049d1e63	FAMU VS BCU	2026-11-03 11:46:19.889201+00	BCU	FAMU	home	BCU	0	True
-- 97db8a95-8345-bd4d-10ed-9ec309e1df78	TRIN @ COL	2026-11-03 11:44:54.660215+00	COL	TRIN	away	TRIN	1	False
-- 97db8a95-8345-bd4d-10ed-9ec309e1df78	TRIN @ COL	2026-11-03 11:44:54.660215+00	COL	TRIN	home	COL	0	True
-- 9a8515fb-d9ea-397c-1820-e612c004cfc1	WIRF @ WISO	2026-11-03 11:43:38.884859+00	WISO	WIRF	away	WIRF	1	False
-- 9a8515fb-d9ea-397c-1820-e612c004cfc1	WIRF @ WISO	2026-11-03 11:43:38.884859+00	WISO	WIRF	home	WISO	0	True
-- d2628798-7870-1050-5fe0-8f0ccd9f0578	MOR VS JOH	2026-11-03 11:44:12.116231+00	JOH	MOR	away	MOR	1	False
-- d2628798-7870-1050-5fe0-8f0ccd9f0578	MOR VS JOH	2026-11-03 11:44:12.116231+00	JOH	MOR	home	JOH	0	True
-- ee5eea3e-5ef7-9624-f80d-f30e5f0117ca	KYST VS FRA	2026-11-03 11:42:22.613867+00	FRA	KYST	away	KYST	1	True
-- ee5eea3e-5ef7-9624-f80d-f30e5f0117ca	KYST VS FRA	2026-11-03 11:42:22.613867+00	FRA	KYST	home	FRA	0	False