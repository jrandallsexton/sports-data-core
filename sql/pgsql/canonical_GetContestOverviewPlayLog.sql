-- SELECT json_agg(row_to_json(t))
-- FROM (
    select
        cp."SequenceNumber" as "Ordinal",
        cp."PeriodNumber" as "Quarter",
        f."Name" as "Team",
        cp."Text" as "Description",
        cp."ClockDisplayValue" as "TimeRemaining",
        cp."ScoringPlay" as "IsScoringPlay",
        cp."Priority" as "IsKeyPlay"
    from public."CompetitionPlay" cp
    inner join public."Competition" co on co."Id" = cp."CompetitionId"
    inner join public."Contest" c on c."Id" = co."ContestId"
    inner join public."FranchiseSeason" fs on fs."Id" = cp."StartTeamFranchiseSeasonId"
    inner join public."Franchise" f on f."Id" = fs."FranchiseId"
    where co."ContestId" = 'b6cde160-f48d-9d51-784b-56bf4adb990a'
    order by cp."SequenceNumber"
--) t;
--select * from public."CompetitionPlay" where "Text" like '%LSU%'
SELECT * from public."CompetitionCompetitorStatistics" where "CompetitionId" = 'cbd0708c-b707-8cc5-bd09-d84856f24d2d'
SELECT * from public."CompetitionCompetitorStatisticCategories" where "CompetitionCompetitorStatisticId" = 'd7e2f37e-02b4-9917-99e3-21197acbf638'
select * from public."CompetitionCompetitorStatisticStats" where "CompetitionCompetitorStatisticCategoryId" = '1da7d0e2-c0af-4399-9d4a-6b36ed92a567' order by "Name"
SELECT * from public."CompetitionCompetitor" where "CompetitionId" = 'cbd0708c-b707-8cc5-bd09-d84856f24d2d'

UPDATE "CompetitionCompetitorStatistics" s
SET "CompetitionCompetitorId" = cc."Id"
FROM "CompetitionCompetitor" cc
WHERE s."CompetitionCompetitorId" IS NULL
  AND cc."FranchiseSeasonId" = s."FranchiseSeasonId"
  AND cc."CompetitionId"      = s."CompetitionId";

  SELECT COUNT(*) AS remaining_nulls
FROM "CompetitionCompetitorStatistics"
WHERE "CompetitionCompetitorId" IS NULL;
-- WITH plays AS (
--   SELECT cp."Type", cp."Text", cp."SequenceNumber"
--   FROM public."CompetitionPlay" cp
--   JOIN public."Competition"      co ON co."Id"  = cp."CompetitionId"
-- )
-- SELECT DISTINCT ON (p."Type")
--        p."Type",
--        p."Text" AS sample_text
-- FROM plays p
-- ORDER BY p."Type", p."SequenceNumber";   -- picks the earliest example per Type

-- WITH plays AS (
--   SELECT cp."Type", cp."Text", cp."SequenceNumber"
--   FROM public."CompetitionPlay" cp
--   JOIN public."Competition"      co ON co."Id"  = cp."CompetitionId"
-- ),
-- first_example AS (
--   SELECT DISTINCT ON (p."Type") p."Type", p."Text" AS sample_text
--   FROM plays p
--   ORDER BY p."Type", p."SequenceNumber"
-- )
-- SELECT p."Type",
--        COUNT(*)                AS play_count,
--        fe.sample_text
-- FROM plays p
-- JOIN first_example fe USING ("Type")
-- GROUP BY p."Type", fe.sample_text
-- ORDER BY p."Type";

-- WITH all_codes AS (
--   SELECT DISTINCT cp."Type"
--   FROM public."CompetitionPlay" cp
-- ),
-- known AS (
--   -- list the int codes you map (keep in sync with the switch)
--   SELECT UNNEST(ARRAY[
--     2,3,5,7,8,9,12,17,18,20,21,24,26,29,32,36,37,39,40,52,53,57,59,60,65,66,67,68,70,79,9999
--   ]) AS code
-- )
-- SELECT ac."Type" AS unknown_type
-- FROM all_codes ac
-- LEFT JOIN known k ON k.code = ac."Type"
-- WHERE k.code IS NULL
-- ORDER BY ac."Type";


