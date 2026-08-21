-- =============================================================================
-- Purge mislabeled 2026-row athlete season statistics (NCAAFB)
-- =============================================================================
-- Context: docs/features/player-pickem/athlete-season-stats-audit-2026-08.md
-- ESPN's 2026 athlete documents hand out the PRIOR season's statistics ref
-- until the new season has data, so every stat doc attached to a 2026
-- AthleteSeason row actually contains 2025 numbers. The season guard (#657)
-- prevents re-pollution; the type-scoped backfill endpoint re-sources the
-- same data onto the correct 2025 rows.
--
-- WHY the purge scope is "everything on a 2026 row" rather than an
-- identity-matched subset: no 2026 games have been played, so a 2026
-- roster row cannot legitimately carry ANY season statistics. Step 0
-- asserts that invariant before anything is deleted — once real 2026
-- games finalize, this script is no longer safe to run as-is and step 0
-- will say so.
--
-- Run order: deploy the backfill PR -> step 0-3 (this purge) -> trigger
-- the backfill (Bruno: athletes-source-statistics) -> AFTER the batch
-- completes, re-run steps 4-5 as the post-backfill verification.
--
-- The FK chain AthleteSeasonStatistic -> Category -> Stat is ON DELETE
-- CASCADE, but the deletes below are explicit child-first anyway so each
-- level reports its own row count.
-- =============================================================================

-- 0) PRE-FLIGHT GUARD: the purge is only valid while no 2026 game has been
--    finalized. Expect zero — if this returns ANY rows, STOP: the blanket
--    2026 purge is no longer provably safe and needs an identity-scoped
--    variant instead.
select count(*) as finalized_2026_contests_MUST_BE_ZERO
from public."Contest"
where "SeasonYear" = 2026
  and "FinalizedUtc" is not null;

-- 1) Pre-counts: what will be deleted. Athletes and roster rows counted
--    separately (mid-season transfers can give one athlete two roster rows
--    in a single season, so the two metrics legitimately differ).
select count(distinct aths."AthleteId")       as athletes_2026_with_stats,
       count(distinct ass."AthleteSeasonId")  as roster_rows_2026_with_stats,
       count(distinct ass."Id")               as statistic_docs
from public."AthleteSeasonStatistic" ass
join public."AthleteSeason" aths on aths."Id" = ass."AthleteSeasonId"
join public."FranchiseSeason" fs on fs."Id" = aths."FranchiseSeasonId"
where fs."SeasonYear" = 2026;

-- 2) Baseline: 2025 coverage BEFORE backfill (expect ~1.9k athletes).
select count(distinct aths."AthleteId")      as athletes_2025_with_stats,
       count(distinct ass."AthleteSeasonId") as roster_rows_2025_with_stats
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

-- =============================================================================
-- POST-BACKFILL VERIFICATION — run steps 4-5 twice: once immediately after
-- the purge (4 should be zero; 5's 2025 row still sparse), and AGAIN after
-- the backfill batch completes in Seq (5's 2025 coverage should approach
-- the active-2025 roster count; 2026 stays zero until real 2026 games).
-- =============================================================================

-- 4) Expect zero after the purge, and STILL zero after the backfill (the
--    season guard must not attach anything to 2026 rows).
select count(*) as remaining_2026_stat_docs
from public."AthleteSeasonStatistic" ass
join public."AthleteSeason" aths on aths."Id" = ass."AthleteSeasonId"
join public."FranchiseSeason" fs on fs."Id" = aths."FranchiseSeasonId"
where fs."SeasonYear" = 2026;

-- 5) Coverage by season + Arch Manning spot check: after the backfill his
--    2025 row should carry his real 2025 stats; his 2026 row stays empty.
select fs."SeasonYear",
       count(distinct aths."AthleteId") as athletes_with_stats
from public."AthleteSeasonStatistic" ass
join public."AthleteSeason" aths on aths."Id" = ass."AthleteSeasonId"
join public."FranchiseSeason" fs on fs."Id" = aths."FranchiseSeasonId"
where fs."SeasonYear" in (2024, 2025, 2026)
group by fs."SeasonYear"
order by fs."SeasonYear" desc;

select fs."SeasonYear", count(ass."Id") as stat_docs
from public."AthleteSeason" aths
join public."FranchiseSeason" fs on fs."Id" = aths."FranchiseSeasonId"
left join public."AthleteSeasonStatistic" ass on ass."AthleteSeasonId" = aths."Id"
where aths."AthleteId" = 'ea6ec41d-31a1-623e-1a68-d21910f17bb8'
group by fs."SeasonYear"
order by fs."SeasonYear" desc;
