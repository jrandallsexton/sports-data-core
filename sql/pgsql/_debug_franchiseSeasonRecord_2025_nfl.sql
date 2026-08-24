
select * from public."Franchise" where "Slug" = 'cincinnati-bengals';

select * from public."FranchiseSeason" where "FranchiseId" = 'be43aed1-bb08-8078-1c9e-8f2a0128061f' order by "SeasonYear" desc; -- 3bf0a8c2-9d7e-06ff-b3ad-a1a0414135d1 2025

select count(*) from public."FranchiseSeason"

select * from public."FranchiseSeasonRecord" where "FranchiseSeasonId" = '3bf0a8c2-9d7e-06ff-b3ad-a1a0414135d1'; -- Cincy 2025

select * from public."FranchiseSeasonRecord" where "FranchiseId" = 'd2ca25ce-337e-1913-b405-69a16329efe7';

select count(*) from public."FranchiseSeasonRecord" -- 5,825

select * from public."Contest" where "SeasonYear" = 2025 and "HomeTeamFranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464' order by "StartDateUtc"; -- LSU 2025

SELECT
      c."Id",
      c."SeasonYear",
      c."StartDateUtc",
      c."EndDateUtc",
      c."FinalizedUtc",
      (c."FinalizedUtc" - c."EndDateUtc")                                          AS "FinalizeLag",
      ROUND(EXTRACT(EPOCH FROM (c."FinalizedUtc" - c."EndDateUtc")) / 86400.0, 1)  AS "FinalizeLagDays"
  FROM public."Contest" c
  WHERE c."SeasonYear" = 2025
    AND c."FinalizedUtc" IS NOT NULL
    AND c."EndDateUtc"   IS NOT NULL
    AND c."FinalizedUtc" > c."EndDateUtc" + INTERVAL '7 days'
  ORDER BY "FinalizeLagDays"

  UPDATE public."Contest"
  SET "FinalizedUtc" = "EndDateUtc" + INTERVAL '1 hour'
  WHERE "SeasonYear" = 2025
    AND "EndDateUtc" IS NOT NULL
    AND isfinite("EndDateUtc")
    AND "FinalizedUtc" IS NOT NULL
    AND "FinalizedUtc" > "EndDateUtc" + INTERVAL '1 day';

select * from public."Competition" where "ContestId" = 'f8112f9a-1c5f-2c72-2c20-d49c467c1a58'; -- TAM @ LSU 2025

select * from public."CompetitionPlay" where "CompetitionId" = 'c972a086-350c-eaab-627b-2c9b530872e8' order by "SequenceNumber"; -- TAM @ LSU 2025

select * from public."CompetitionPlayExternalId" where "CompetitionPlayId" in ('a97721af-6414-aea0-5615-856d5905a785', 'd3a3cf3b-fee7-8e0b-597a-19d780458da9');
select * from public."CompetitionPlayExternalId" where "SourceUrlHash" = '1e682b7e507ecd4493de892c05449ab06e030a8181f69785d04a442e4171e190';

select * from public."CompetitionCompetitor" where "CompetitionId" = 'e2c08e9e-f40d-4303-770e-3b8592aa17d7'; -- ARK @ LSU 2025

-- Id	CompetitionId	FranchiseSeasonId	Type	Order	HomeAway	Winner	CuratedRankCurrent	CreatedUtc	ModifiedUtc	CreatedBy	ModifiedBy	Points	Discriminator
-- 79c19b4d-1b46-cfa5-3390-be0d86bcb5ef	e2c08e9e-f40d-4303-770e-3b8592aa17d7	c13b7c74-6892-3efa-2492-36ebf5220464	team	0	home	False	9	2025-08-25 12:36:51.354947+00	NULL	47b7dc35-7db8-4650-b6b0-64d81fb384b3	NULL	NULL	FootballCompetitionCompetitor
-- 53c5ce32-694f-6bfa-b426-d15c87938b2f	e2c08e9e-f40d-4303-770e-3b8592aa17d7	4aa7cc0c-e63f-feba-ca78-0f18a22f2576	team	1	away	False	99	2025-08-25 12:36:51.764236+00	NULL	d9c9059f-b8bc-4991-8f7e-11c12f8b568e	NULL	NULL	FootballCompetitionCompetitor

select count(*) from public."CompetitionCompetitorRecord";

select * from public."CompetitionCompetitorRecord" where "CompetitionCompetitorId" = '53c5ce32-694f-6bfa-b426-d15c87938b2f'; -- ARK 2025

-- Id	CompetitionCompetitorId	Type	Name	Summary	DisplayValue	Value	CreatedUtc	ModifiedUtc	CreatedBy	ModifiedBy
-- 31809a70-1449-4ddf-be60-2d5b5b4f8dfe	80e3500e-63a8-701a-8471-0109bf78a6db	road	Road	2-2	2-2	0.5	2026-02-27 00:19:31.502895-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL
-- f389e483-6e3a-4a75-b8ae-94b2565e1396	80e3500e-63a8-701a-8471-0109bf78a6db	home	Home	5-0	5-0	1	2026-02-27 00:19:31.46071-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL
-- 6b056419-83f4-40f7-9a12-64dcc5bf066f	80e3500e-63a8-701a-8471-0109bf78a6db	total	overall	7-2	7-2	0.7777777777777778	2026-02-27 00:19:31.455239-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL
-- e893f7eb-e2e4-49e8-a70c-a0c64bd7dabc	80e3500e-63a8-701a-8471-0109bf78a6db	vsconf	vs. Conf.	4-2	4-2	0.6666666666666666	2026-02-27 00:19:31.495909-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL

select * from public."CompetitionCompetitorRecord" where "CompetitionCompetitorId" = '79c19b4d-1b46-cfa5-3390-be0d86bcb5ef'; -- LSU 2025

-- Id	CompetitionCompetitorId	Type	Name	Summary	DisplayValue	Value	CreatedUtc	ModifiedUtc	CreatedBy	ModifiedBy
-- ffdd007a-b972-4cdb-bb0b-e74b0486ccfd	dbdbf477-edae-dc06-996c-edbcb264efc8	road	Road	2-1	2-1	0.6666666666666666	2026-02-27 00:19:31.339797-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL
-- 6e0836c6-b72b-454d-bd4d-c3cf54d5263a	dbdbf477-edae-dc06-996c-edbcb264efc8	home	Home	4-1	4-1	0.8	2026-02-27 00:19:31.322044-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL
-- 030e841a-b70b-401d-95ca-92a87cf3c4cb	dbdbf477-edae-dc06-996c-edbcb264efc8	total	overall	6-3	6-3	0.6666666666666666	2026-02-27 00:19:31.248249-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL
-- e0c33385-dc58-41f0-ad3a-42abf46a7238	dbdbf477-edae-dc06-996c-edbcb264efc8	vsconf	vs. Conf.	3-2	3-2	0.6	2026-02-27 00:19:31.382692-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL

-- That's the right instinct — a missing EndDateUtc is data telling you the game may never have been played (postponed/cancelled), so
--   estimating an end would fabricate a result. Split into two:

--   (a) Investigation list — the no-end games. And before you even hit ESPN, we already store the competition status, which may answer it
--   outright (a STATUS_POSTPONED / STATUS_CANCELED there is your answer). This also hands you the ESPN ref URL to click through:

  SELECT
      c."Id"               AS "ContestId",
      c."StartDateUtc",
      c."EndDateUtc",
      c."FinalizedUtc",
      cs."StatusTypeName"  AS "Status",
      cs."StatusDescription",
      fAway."DisplayName"  AS "Away",
      fHome."DisplayName"  AS "Home",
      cxi."SourceUrl"      AS "EspnRef"
  FROM public."Contest" c
  INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
  INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
  INNER JOIN public."Franchise" fAway ON fAway."Id" = fsAway."FranchiseId"
  INNER JOIN public."Franchise" fHome ON fHome."Id" = fsHome."FranchiseId"
  LEFT JOIN public."Competition"       comp ON comp."ContestId"   = c."Id"
  LEFT JOIN public."CompetitionStatus" cs   ON cs."CompetitionId" = comp."Id"
  LEFT JOIN public."ContestExternalId" cxi  ON cxi."ContestId"    = c."Id"
  WHERE c."SeasonYear" = 2025
    AND (c."EndDateUtc" IS NULL OR NOT isfinite(c."EndDateUtc"))
  ORDER BY c."StartDateUtc";

--   (isfinite(NULL) is NULL, so the explicit IS NULL OR NOT isfinite(...) catches both true-null and -infinity. LEFT joins on
--   status/externalid so a cancelled game with no competition row still shows up rather than silently dropping.)

--   (b) Correction — only the games that actually finished. Now gated on a finite, present EndDateUtc, so the no-end games are left untouched
--   for your investigation:

  BEGIN;

  -- 0. Snapshot exactly the rows we'll change (reversible).
  CREATE TABLE contest_finalize_fix_backup_20260724 AS
  SELECT "Id", "StartDateUtc", "EndDateUtc", "FinalizedUtc"
  FROM public."Contest"
  WHERE "SeasonYear" = 2025
    AND "FinalizedUtc" IS NOT NULL
    AND "EndDateUtc"   IS NOT NULL
    AND isfinite("EndDateUtc")
    AND "FinalizedUtc" > "EndDateUtc" + INTERVAL '1 day';

  -- 1. Reset lagged FinalizedUtc to the game's actual end time.
  UPDATE public."Contest"
  SET "FinalizedUtc" = "EndDateUtc"
  WHERE "SeasonYear" = 2025
    AND "FinalizedUtc" IS NOT NULL
    AND "EndDateUtc"   IS NOT NULL
    AND isfinite("EndDateUtc")
    AND "FinalizedUtc" > "EndDateUtc" + INTERVAL '1 day';

  -- 2. Verify.
  SELECT count(*) AS still_lagging
  FROM public."Contest"
  WHERE "SeasonYear" = 2025
    AND "FinalizedUtc" IS NOT NULL
    AND "EndDateUtc"   IS NOT NULL
    AND isfinite("EndDateUtc")
    AND "FinalizedUtc" > "EndDateUtc" + INTERVAL '1 day';   -- expect 0

  -- COMMIT;   -- if still_lagging = 0 and the backup row count matches what you expect
  -- ROLLBACK; -- otherwise

--   Changes from before:
--   - Dropped the EndDateUtc estimate entirely — no fabricated end times. So the 3.5h/sport-duration question is moot now.
--   - Threshold is now FinalizedUtc > EndDateUtc + 1 day (finalized more than a day after the game ended) — tighter and more meaningful than
--   anchoring on kickoff, and it only bites the batch artifacts, never a normally-processed game (those finalize minutes-to-hours after end,
--   well under a day).
--   - Sets FinalizedUtc = EndDateUtc. Add + INTERVAL '30 minutes' if you'd rather it read as just-after-end than exactly-at-end (cosmetic;
--   irrelevant to the mini-schedule).

--   Run (a), triage what it surfaces (status column first), and run (b) whenever — they're independent. When you've worked through the no-end
--   games in ESPN and know whether they're postponed/cancelled, I can help with whatever correction those need (status fix, leaving them
--   unfinalized, etc.).