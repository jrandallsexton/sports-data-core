-- =============================================================================
-- Purge mislabeled 2026-row athlete season statistics (NCAAFB)
-- =============================================================================
-- Context: docs/features/player-pickem/athlete-season-stats-audit-2026-08.md
-- ESPN's 2026 athlete documents hand out the PRIOR season's statistics ref
-- until the new season has data, so every stat doc attached to a 2026
-- AthleteSeason row actually contains 2025 numbers (~15,263 athletes).
-- The season guard (#657) prevents re-pollution; the type-scoped backfill
-- endpoint re-sources the same data onto the correct 2025 rows.
--
-- Run order: deploy the backfill PR -> run THIS purge -> trigger the
-- backfill (Bruno: athletes-source-statistics).
--
-- The FK chain AthleteSeasonStatistic -> Category -> Stat is ON DELETE
-- CASCADE, but the deletes below are explicit child-first anyway so each
-- level reports its own row count.
-- =============================================================================

-- 1) Pre-counts: what will be deleted
select count(distinct ass."AthleteSeasonId") as athletes_2026_with_stats,
       count(distinct ass."Id")              as statistic_docs
from public."AthleteSeasonStatistic" ass
join public."AthleteSeason" aths on aths."Id" = ass."AthleteSeasonId"
join public."FranchiseSeason" fs on fs."Id" = aths."FranchiseSeasonId"
where fs."SeasonYear" = 2026;

-- 2) Sanity: 2025 rows BEFORE backfill (expect ~1,942 athletes; re-run
--    after the backfill and expect it near the active-2025 roster count)
select count(distinct ass."AthleteSeasonId") as athletes_2025_with_stats
from public."AthleteSeasonStatistic" ass
join public."AthleteSeason" aths on aths."Id" = ass."AthleteSeasonId"
join public."FranchiseSeason" fs on fs."Id" = aths."FranchiseSeasonId"
where fs."SeasonYear" = 2025;

-- 3) The purge (single transaction, child-first)
begin;

create temp table _purge_stat_docs on commit drop as
select ass."Id"
from public."AthleteSeasonStatistic" ass
join public."AthleteSeason" aths on aths."Id" = ass."AthleteSeasonId"
join public."FranchiseSeason" fs on fs."Id" = aths."FranchiseSeasonId"
where fs."SeasonYear" = 2026;

delete from public."AthleteSeasonStatisticStat" s
using public."AthleteSeasonStatisticCategory" c, _purge_stat_docs p
where s."AthleteSeasonStatisticCategoryId" = c."Id"
  and c."AthleteSeasonStatisticId" = p."Id";

delete from public."AthleteSeasonStatisticCategory" c
using _purge_stat_docs p
where c."AthleteSeasonStatisticId" = p."Id";

delete from public."AthleteSeasonStatistic" ass
using _purge_stat_docs p
where ass."Id" = p."Id";

commit;

-- 4) Verify: expect zero
select count(*) as remaining_2026_stat_docs
from public."AthleteSeasonStatistic" ass
join public."AthleteSeason" aths on aths."Id" = ass."AthleteSeasonId"
join public."FranchiseSeason" fs on fs."Id" = aths."FranchiseSeasonId"
where fs."SeasonYear" = 2026;

-- 5) Post-backfill spot check: Arch Manning's 2025 row should carry his
--    real 2025 stats; his 2026 row should be empty until 2026 games exist.
select fs."SeasonYear", count(ass."Id") as stat_docs
from public."AthleteSeason" aths
join public."FranchiseSeason" fs on fs."Id" = aths."FranchiseSeasonId"
left join public."AthleteSeasonStatistic" ass on ass."AthleteSeasonId" = aths."Id"
where aths."AthleteId" = 'ea6ec41d-31a1-623e-1a68-d21910f17bb8'
group by fs."SeasonYear"
order by fs."SeasonYear" desc;
