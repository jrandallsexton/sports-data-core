-- Winner-consistency triage, round 2 (2026-09-03).
-- Summary told us: 14 live 2026 rows (10 wrong-side + 4 null-winner);
-- 2014-2019 nulls are backfill-era derivation gaps (separate campaign).
--
-- QUERY 1: the 2026 hit list, with the CompetitionCompetitorScore
-- cross-check that decides the remedy PER ROW:
--   CcsAgreesWithScores = true  -> inputs already healed; REENRICH alone
--                                  fixes it (POST /admin/contest/{id}/reenrich)
--   CcsAgreesWithScores = false -> inputs still stale; REFRESH first
--                                  (re-source), then reenrich. Reenriching
--                                  first would re-derive from stale inputs.
-- EspnSaysAwayWon/HomeWon = ESPN's own Winner flag on the score rows —
-- the independent source of truth for eyeballing each verdict.

WITH bad2026 AS (
  SELECT c.*
  FROM public."Contest" c
  WHERE c."FinalizedUtc" IS NOT NULL
    AND c."StartDateUtc" >= '2026-01-01'
    AND c."AwayScore" IS NOT NULL
    AND c."HomeScore" IS NOT NULL
    AND c."AwayScore" <> c."HomeScore"
    AND (
          (c."AwayScore" > c."HomeScore" AND c."WinnerFranchiseSeasonId" IS DISTINCT FROM c."AwayTeamFranchiseSeasonId")
       OR (c."HomeScore" > c."AwayScore" AND c."WinnerFranchiseSeasonId" IS DISTINCT FROM c."HomeTeamFranchiseSeasonId")
    )
),
ccs AS (
  SELECT comp."ContestId",
         cc."HomeAway",
         MAX(s."Value")     AS "MaxScore",
         BOOL_OR(s."Winner") AS "EspnWinnerFlag"
  FROM public."Competition" comp
  JOIN public."CompetitionCompetitor" cc ON cc."CompetitionId" = comp."Id"
  LEFT JOIN public."CompetitionCompetitorScores" s ON s."CompetitionCompetitorId" = cc."Id"
  WHERE comp."ContestId" IN (SELECT "Id" FROM bad2026)
  GROUP BY comp."ContestId", cc."HomeAway"
)
SELECT
  b."Id" AS "ContestId",
  b."StartDateUtc",
  fA."Slug" AS "Away",
  fH."Slug" AS "Home",
  b."AwayScore",
  b."HomeScore",
  CASE
    WHEN b."WinnerFranchiseSeasonId" = b."AwayTeamFranchiseSeasonId" THEN 'away'
    WHEN b."WinnerFranchiseSeasonId" = b."HomeTeamFranchiseSeasonId" THEN 'home'
    ELSE '(null)'
  END AS "RecordedWinner",
  ccsA."MaxScore" AS "CcsAway",
  ccsH."MaxScore" AS "CcsHome",
  ccsA."EspnWinnerFlag" AS "EspnSaysAwayWon",
  ccsH."EspnWinnerFlag" AS "EspnSaysHomeWon",
  (b."AwayScore" = ccsA."MaxScore" AND b."HomeScore" = ccsH."MaxScore") AS "CcsAgreesWithScores",
  b."AuditedUtc",
  b."FinalizedUtc"
FROM bad2026 b
JOIN public."FranchiseSeason" fsA ON fsA."Id" = b."AwayTeamFranchiseSeasonId"
JOIN public."Franchise" fA ON fA."Id" = fsA."FranchiseId"
JOIN public."FranchiseSeason" fsH ON fsH."Id" = b."HomeTeamFranchiseSeasonId"
JOIN public."Franchise" fH ON fH."Id" = fsH."FranchiseId"
LEFT JOIN ccs ccsA ON ccsA."ContestId" = b."Id" AND ccsA."HomeAway" = 'away'
LEFT JOIN ccs ccsH ON ccsH."ContestId" = b."Id" AND ccsH."HomeAway" = 'home'
ORDER BY b."StartDateUtc";


-- QUERY 2: preview-reset candidates — UPCOMING contests (next 14 days)
-- involving any team from a bad 2026 row. Their preview payloads may
-- embed the phantom result (the Idaho @ Utah case). For each ContestId
-- returned that has a generated MatchupPreview in the API DB:
-- resetPreview + regenerate AFTER the Query-1 row is healed; rerun the
-- model-lab panel for any that have matrix cells.

WITH bad2026 AS (
  SELECT c.*
  FROM public."Contest" c
  WHERE c."FinalizedUtc" IS NOT NULL
    AND c."StartDateUtc" >= '2026-01-01'
    AND c."AwayScore" IS NOT NULL
    AND c."HomeScore" IS NOT NULL
    AND c."AwayScore" <> c."HomeScore"
    AND (
          (c."AwayScore" > c."HomeScore" AND c."WinnerFranchiseSeasonId" IS DISTINCT FROM c."AwayTeamFranchiseSeasonId")
       OR (c."HomeScore" > c."AwayScore" AND c."WinnerFranchiseSeasonId" IS DISTINCT FROM c."HomeTeamFranchiseSeasonId")
    )
),
affected_teams AS (
  SELECT "AwayTeamFranchiseSeasonId" AS fsid FROM bad2026
  UNION
  SELECT "HomeTeamFranchiseSeasonId" FROM bad2026
)
SELECT
  c."Id" AS "ContestId",
  c."StartDateUtc",
  fA."Slug" AS "Away",
  fH."Slug" AS "Home"
FROM public."Contest" c
JOIN public."FranchiseSeason" fsA ON fsA."Id" = c."AwayTeamFranchiseSeasonId"
JOIN public."Franchise" fA ON fA."Id" = fsA."FranchiseId"
JOIN public."FranchiseSeason" fsH ON fsH."Id" = c."HomeTeamFranchiseSeasonId"
JOIN public."Franchise" fH ON fH."Id" = fsH."FranchiseId"
WHERE c."StartDateUtc" > NOW() AT TIME ZONE 'utc'
  AND c."StartDateUtc" < (NOW() AT TIME ZONE 'utc') + INTERVAL '14 days'
  AND (c."AwayTeamFranchiseSeasonId" IN (SELECT fsid FROM affected_teams)
    OR c."HomeTeamFranchiseSeasonId" IN (SELECT fsid FROM affected_teams))
ORDER BY c."StartDateUtc";

-- ContestId	StartDateUtc	Away	Home	AwayScore	HomeScore	RecordedWinner	CcsAway	CcsHome	EspnSaysAwayWon	EspnSaysHomeWon	CcsAgreesWithScores	AuditedUtc	FinalizedUtc
-- e47f2279-fe9d-b458-7268-1dcd21a37ae7	2026-08-27 22:00:00+00	ohio-dominican-panthers	morehead-state-eagles	42	40	home	42	40	True	False	True	NULL	2026-08-31 00:35:14.744356+00
-- 26d051e8-8959-7d86-9b60-423c4bbddcec	2026-08-27 22:00:00+00	stony-brook-seawolves	delaware-state-hornets	34	41	(null)	34	41	False	False	True	NULL	2026-08-30 21:27:57.620093+00
-- 8a74a1a2-f5a2-fef0-82f2-28e8d2bb5fd6	2026-08-27 23:00:00+00	mississippi-valley-state-delta-devils	nicholls-colonels	10	44	away	10	44	False	False	True	NULL	2026-08-30 23:14:41.748819+00
-- fd5136db-abd8-8807-18dd-55552662112c	2026-08-27 23:00:00+00	uva-wise-cavaliers	presbyterian-blue-hose	37	47	away	37	47	False	False	True	NULL	2026-08-30 23:13:12.631255+00
-- 41438bb9-6add-5657-4460-9327d130debe	2026-08-29 16:00:00+00	north-carolina-tar-heels	tcu-horned-frogs	15	10	(null)	15	10	False	False	True	NULL	2026-08-31 00:20:05.033573+00
-- 4517177f-22be-2b54-0580-d3b767bb873d	2026-08-29 17:00:00+00	georgetown-college-kentucky-tigers	butler-bulldogs	23	27	away	23	27	False	False	True	NULL	2026-08-31 00:26:42.70615+00
-- b6cea518-e41c-7ee9-8445-4406cf1d3827	2026-08-29 17:00:00+00	roosevelt-lakers	chicago-state-cougars	44	28	home	44	28	False	False	True	NULL	2026-08-31 00:29:19.670209+00
-- c8c5e973-8aad-8ebd-6dd9-a859633a4914	2026-08-29 18:00:00+00	lawrence-tech-blue-devils	valparaiso-beacons	13	59	away	13	59	False	False	True	NULL	2026-08-31 00:28:46.98454+00
-- 6a7c0e75-cfef-7b3e-e54d-a1922a3c6b99	2026-08-29 19:00:00+00	alabama-state-hornets	southern-jaguars	30	17	home	17	30	False	False	False	NULL	2026-08-31 00:24:41.507392+00
-- 57f96bdc-91a3-32b5-0d5e-5b18f6d7bb6e	2026-08-29 19:00:00+00	uc-davis-aggies	portland-state-vikings	31	24	(null)	31	24	False	False	True	NULL	2026-08-31 00:20:05.718752+00
-- 764dc527-98fe-2695-c366-abb3ae883e93	2026-08-29 21:00:00+00	eastern-washington-eagles	northern-arizona-lumberjacks	27	34	away	27	34	False	False	True	NULL	2026-08-31 00:20:03.180062+00
-- 327fc836-7dbc-bcc5-cdc6-e10829d31296	2026-08-29 22:00:00+00	southeast-missouri-state-redhawks	indiana-state-sycamores	23	21	(null)	23	21	False	False	True	NULL	2026-08-31 00:21:28.705564+00
-- c82f6189-cec1-f55d-ae8c-adab3d5f2f33	2026-08-29 23:00:00+00	north-carolina-central-eagles	texas-southern-tigers	30	42	away	30	42	False	False	True	NULL	2026-08-31 00:25:25.546284+00
-- 1064ad09-1507-57f8-5851-2cccb72c3e67	2026-08-29 23:30:00+00	howard-bison	alabama-am-bulldogs	31	24	home	24	31	False	False	False	NULL	2026-08-31 00:23:00.581754+00

-- ContestId	StartDateUtc	Away	Home
-- 22c1054c-41ca-5311-f61c-b17c6eea1711	2026-09-03 23:00:00+00	lindenwood-lions	stony-brook-seawolves
-- 97196018-8ccd-f82d-30c8-86f24919533f	2026-09-03 23:00:00+00	ohio-dominican-panthers	edinboro-university-fighting-scots
-- e666c020-d9a2-98f9-55be-01a496f9d0c9	2026-09-04 23:00:00+00	indiana-state-sycamores	purdue-boilermakers
-- 13adbfe0-c177-026a-c57c-f12f66df6201	2026-09-05 17:00:00+00	southeast-missouri-state-redhawks	iowa-state-cyclones
-- b0c0cf60-803e-077a-d4d7-0788427431f4	2026-09-05 22:00:00+00	presbyterian-blue-hose	mercer-bears
-- da18cb60-24a4-65c9-0cfa-a632dcde92c3	2026-09-05 22:00:00+00	lane-college-dragons	alabama-state-hornets
-- 41c0c73e-319a-b450-b97c-5876b518c684	2026-09-05 22:00:00+00	delaware-state-hornets	william-mary-tribe
-- 0070b256-cc15-bdb4-22e4-8e60e64ed736	2026-09-05 22:00:00+00	elizabeth-city-state-vikings	north-carolina-central-eagles
-- 677969ab-c1ca-9432-5ba6-e440097a6966	2026-09-05 22:00:00+00	richmond-spiders	howard-bison
-- af1be31f-a453-6113-8c8c-789edfe8fc7a	2026-09-05 23:00:00+00	nicholls-colonels	kansas-state-wildcats
-- 99dfd5a4-f75f-41e3-7c29-8cdc95f0ece7	2026-09-05 23:00:00+00	miles-college-golden-bears	alabama-am-bulldogs
-- 74808bd3-213e-f5e6-897e-42046d722f4f	2026-09-05 23:00:00+00	northern-iowa-panthers	eastern-washington-eagles
-- 07066cf5-61a6-50d1-10ff-934ca4be369e	2026-09-05 23:00:00+00	kentucky-state-thorobreds	southern-jaguars
-- 941fce47-617c-b76c-a9c1-0238d049cb7e	2026-09-05 23:00:00+00	chicago-state-cougars	ut-martin-skyhawks
-- f31da43e-39c5-2da1-295f-cb71022b16b1	2026-09-06 00:30:00+00	butler-bulldogs	montana-state-bobcats
-- 8b9db883-e98e-82cc-7f63-5a873c7a5758	2026-09-06 01:30:00+00	portland-state-vikings	san-diego-state-aztecs
-- e712aa02-3ef8-e843-9944-2442657a36ad	2026-09-06 01:30:00+00	northern-arizona-lumberjacks	arizona-wildcats
-- 6f92b82f-360d-7743-6c5c-c418e9930789	2026-09-06 02:00:00+00	uc-davis-aggies	san-diego-toreros
-- 9cb7b3cd-a722-4aa2-6781-5fa103edd05e	2026-09-06 02:00:00+00	mississippi-valley-state-delta-devils	sacramento-state-hornets
-- 859bf8cc-9f90-0c74-0fcc-8136d3787423	2026-09-06 16:00:00+00	texas-southern-tigers	prairie-view-am-panthers
-- ad9fa305-ef03-320f-b836-7ef092168e47	2026-09-12 04:00:00+00	north-carolina-at-aggies	north-carolina-central-eagles
-- 31c91e18-2b5b-35cc-016c-3236897884ad	2026-09-12 04:00:00+00	texas-southern-tigers	utep-miners
-- 524cb7dd-09db-9fda-179b-5afb56b7de16	2026-09-12 04:00:00+00	tennessee-state-tigers	alabama-am-bulldogs
-- 9c680149-206a-70cd-8d47-f7ec9bb90196	2026-09-12 04:00:00+00	kentucky-christian-knights	chicago-state-cougars
-- 3752fb39-5bd6-7e6d-f6ba-fa57dbc897cc	2026-09-12 04:00:00+00	franklin-grizzlies	butler-bulldogs
-- 6697e776-8246-89c7-c6fa-6118b9964632	2026-09-12 04:00:00+00	indiana-state-sycamores	eastern-illinois-panthers
-- b5f851c0-2969-e222-9b2e-abfb33e67d99	2026-09-12 04:00:00+00	bowie-state-bulldogs	delaware-state-hornets
-- 278c4cd4-4b49-f6bb-e80e-b18a62e3470e	2026-09-12 04:00:00+00	mississippi-valley-state-delta-devils	lincoln-pa-lions
-- ec552812-3746-e4a1-7715-c68a718b91e0	2026-09-12 16:00:00+00	east-tennessee-state-buccaneers	north-carolina-tar-heels
-- af5452e5-03a2-a140-3d60-e292e41d4975	2026-09-12 16:00:00+00	howard-bison	indiana-hoosiers
-- b2530dd2-4414-2ff4-e962-b80c80aea525	2026-09-12 18:00:00+00	eastern-washington-eagles	south-dakota-coyotes
-- 7b1b6af5-88ce-51ac-cd4a-100bac09cd98	2026-09-12 18:00:00+00	stony-brook-seawolves	ball-state-cardinals
-- 4be7f5a7-838f-04c5-9227-e34f8c96eb1f	2026-09-12 20:00:00+00	alabama-state-hornets	troy-trojans
-- b72bd83d-ab6e-1524-ee18-5df710ff6e74	2026-09-12 20:00:00+00	uc-davis-aggies	smu-mustangs
-- 8955baba-60fe-8db3-352f-8e3a07aa6ca4	2026-09-12 21:00:00+00	morehead-state-eagles	austin-peay-governors
-- fa967757-4293-1019-6044-669078705a3e	2026-09-12 23:00:00+00	ut-rio-grande-valley-vaqueros	nicholls-colonels
-- 154ac2b3-c822-e30c-b101-28ae0008019d	2026-09-12 23:00:00+00	southeast-missouri-state-redhawks	southern-illinois-salukis
-- 4ef53674-e209-bd90-bd11-c35f39bc8cd6	2026-09-12 23:00:00+00	valparaiso-beacons	murray-state-racers
-- 47b160ee-3b82-fd7d-32dc-13f64386e707	2026-09-12 23:00:00+00	southern-jaguars	houston-cougars
-- 95faed42-15d7-814c-5bc8-c6fc9f0dfd3e	2026-09-12 23:00:00+00	incarnate-word-cardinals	northern-arizona-lumberjacks
-- a9bccc2c-634f-d5c8-d8e1-78c7f927e2e7	2026-09-13 00:00:00+00	grambling-tigers	tcu-horned-frogs
-- 7192b581-15a8-1353-7300-dabfe5b89841	2026-09-13 02:00:00+00	north-dakota-fighting-hawks	portland-state-vikings
