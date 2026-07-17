-- ─── Variables ────────────────────────────────────────────────────────────────
-- Postgres has no T-SQL-style DECLARE at the top level. Two patterns:
--   SET          → literal value
--   set_config() → value from a subquery (T-SQL "SELECT @var = col FROM ...")
-- Strings only; cast at the call site against typed columns.
SET my.contest_id  = '5dbbb652-568b-6707-d295-f1fd251477d7';
SET my.season_id   = '4810b35f-8e31-2631-eba4-ac268341fc43';
SET my.season_year = '2026';

-- Derive competition_id from contest_id. set_config returns the value so the
-- result row also confirms what got stashed. Empty subquery → variable keeps
-- its prior value (silent no-op), so eyeball the output before running
-- downstream queries when retargeting.
SELECT set_config('my.competition_id', "Id"::text, false) AS competition_id
FROM public."Competition"
WHERE "ContestId" = current_setting('my.contest_id')::uuid
LIMIT 1;

-- ─── Health snapshot ──────────────────────────────────────────────────────────
SELECT 'NonFinalizedContests', COUNT(*) FROM public."Contest" WHERE "FinalizedUtc" IS NULL; -- 24,337

-- ─── Contest by id ────────────────────────────────────────────────────────────
SELECT 'Contest', * FROM public."Contest" WHERE "Id" = current_setting('my.contest_id')::uuid;

SELECT 'ContestExternalId', * FROM public."ContestExternalId" WHERE "ContestId" = current_setting('my.contest_id')::uuid;

SELECT 'Competition', * FROM public."Competition" WHERE "ContestId" = current_setting('my.contest_id')::uuid;

-- ─── Competition fan-out (uses derived competition_id) ────────────────────────
SELECT 'CompetitionNote', * FROM public."CompetitionNote" WHERE "CompetitionId" = current_setting('my.competition_id')::uuid;

SELECT 'CompetitionCompetitor', * FROM public."CompetitionCompetitor" WHERE "CompetitionId" = current_setting('my.competition_id')::uuid;

SELECT 'CompetitionCompetitorScores', * FROM public."CompetitionCompetitorScores"
WHERE "CompetitionCompetitorId" IN (
    SELECT "Id" FROM public."CompetitionCompetitor"
    WHERE "CompetitionId" = current_setting('my.competition_id')::uuid
);

-- Column1	Id	CompetitionCompetitorId	Value	DisplayValue	Winner	SourceId	SourceDescription	CreatedUtc	ModifiedUtc	CreatedBy	ModifiedBy
-- CompetitionCompetitorScores	72cec001-e65e-6388-efaf-6cd72000a61b	25c4ef94-1118-3372-a127-c723c628a73e	0	0	False	1	basic/manual	2026-04-20 18:49:58.873879+00	NULL	3f2e28d8-471f-429c-8cc2-8f36ddf16b08	NULL
-- CompetitionCompetitorScores	a10640ef-69a9-36f6-99b1-591473b5399e	25c4ef94-1118-3372-a127-c723c628a73e	8	8	True	2	feed	2026-06-18 03:38:46.413419+00	NULL	e083e222-ef4c-4cdf-84b6-95c0f31a5fd5	NULL
-- CompetitionCompetitorScores	c1271982-00be-048f-f6b8-20723955a08c	31540111-c671-e130-f893-c5a35468c038	0	0	False	1	basic/manual	2026-04-20 18:49:58.895503+00	NULL	3f2e28d8-471f-429c-8cc2-8f36ddf16b08	NULL
-- CompetitionCompetitorScores	c072fad1-d31a-37bc-a48e-46dfdc8576f9	31540111-c671-e130-f893-c5a35468c038	6	6	False	2	feed	2026-06-18 03:38:44.231185+00	NULL	e083e222-ef4c-4cdf-84b6-95c0f31a5fd5	NULL

-- NCAAFB Example
-- Column1	Id	CompetitionCompetitorId	Value	DisplayValue	Winner	SourceId	SourceDescription	CreatedUtc	ModifiedUtc	CreatedBy	ModifiedBy
-- CompetitionCompetitorScores	dc48c9ca-1a61-8203-1b36-2e9b73cc4d49	052ed826-1de5-fe89-6a87-cb20b6cdb487	9	9	False	1	Basic/Manual	2025-11-19 18:34:46.836883+00	2025-12-22 14:03:57.745321+00	e5a246de-3622-4a04-8f70-98fe1973508c	da3c4d9d-9169-44f6-82a1-d0f1b9a5a840
-- CompetitionCompetitorScores	901e12fb-bbf2-c972-f9f7-b08ca1cea6c4	e3f8d89f-1284-69aa-9858-5a6b3774f4bb	42	42	False	1	Basic/Manual	2025-11-19 18:34:45.092117+00	2025-12-22 14:03:48.272355+00	fac2799d-3cfe-400c-849b-d64c0691eda7	da3c4d9d-9169-44f6-82a1-d0f1b9a5a840

SELECT 'CompetitionStatus', * FROM public."CompetitionStatus" WHERE "CompetitionId" = current_setting('my.competition_id')::uuid;

SELECT 'CompetitionStream', * FROM public."CompetitionStream" WHERE "CompetitionId" = current_setting('my.competition_id')::uuid;

SELECT 'CompetitionOdds', * FROM public."CompetitionOdds" WHERE "CompetitionId" = current_setting('my.competition_id')::uuid;

-- select distinct CO."ProviderId", CO."ProviderName" FROM public."CompetitionOdds" CO
-- inner JOIN public."Competition" COMP on COMP."Id" = CO."CompetitionId"
-- inner join public."Contest" C on C."Id" = COMP."ContestId"
-- where c."SeasonYear" in (2022, 2023, 2024, 2025, 2026)
-- order by CO."ProviderName";

-- ─── Lookup tables ────────────────────────────────────────────────────────────
--SELECT DISTINCT "StatusTypeName" FROM public."CompetitionStatus";

--SELECT * FROM public."AthletePosition";
--SELECT * FROM public."AthletePositionExternalId";

--SELECT * FROM public."Season" WHERE "Year" = current_setting('my.season_year')::int;

-- SELECT * FROM public."SeasonWeek"
-- WHERE "SeasonId" = current_setting('my.season_id')::uuid
-- ORDER BY "Number";

-- ─── MatchupResult-style join (mirrors GetMatchupResultByContestId.sql) ───────
SELECT
  c."Id" AS "ContestId",
  c."AwayTeamFranchiseSeasonId" AS "AwayFranchiseSeasonId",
  c."HomeTeamFranchiseSeasonId" AS "HomeFranchiseSeasonId",
  c."SeasonWeekId",
  co."Id" AS "CompetitionId",
  coo."Spread" AS "Spread",
  coo."ProviderName",
  c."AwayScore",
  c."HomeScore",
  c."WinnerFranchiseSeasonId",
  c."SpreadWinnerFranchiseSeasonId",
  c."FinalizedUtc"
FROM public."Contest" c
INNER JOIN public."Competition" co ON co."ContestId" = c."Id"
LEFT JOIN LATERAL (
  SELECT *
  FROM public."CompetitionOdds"
  WHERE "CompetitionId" = co."Id"
    AND "ProviderId" IN ('58', '100')
  ORDER BY CASE WHEN "ProviderId" = '58' THEN 1 ELSE 2 END
  LIMIT 1
) coo ON TRUE
WHERE c."Id" = current_setting('my.contest_id')::uuid;
