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
-- roster row cannot legitimately carry ANY season statistics. Step 2's
-- guard ENFORCES that invariant inside the purge transaction — it raises
-- an exception (aborting the transaction) if any finalized 2026 contest
-- exists. Once real 2026 games finalize, this script refuses to run and
-- an identity-scoped variant is needed instead.
--
-- Concurrency: this script does not stop Producer's statistic writers.
-- That is deliberate — post-#657 a statistics document whose ref parses
-- to a prior season attaches to THAT season's roster row, so organic
-- processing no longer writes to 2026 rows. If a writer nonetheless
-- races the purge (insert lands after _purge_stat_docs is materialized),
-- step 4 catches it: a nonzero result means RE-RUN this script (it is
-- idempotent) before triggering the backfill.
--
-- Run order: deploy the backfill PR -> steps 1-3 (this purge) -> step 4
-- must be zero (nonzero => re-run the purge) -> trigger the backfill
-- (Bruno: athletes-source-statistics) -> AFTER the batch completes,
-- re-run steps 4-5 as the post-backfill verification.
--
-- The FK chain AthleteSeasonStatistic -> Category -> Stat is ON DELETE
-- CASCADE, but the deletes below are explicit child-first anyway so each
-- level reports its own row count.
-- =============================================================================

-- 1) Pre-counts: what will be deleted. Athletes and roster rows counted
--    separately (mid-season transfers can give one athlete two roster rows
--    in a single season, so the two metrics legitimately differ). Also the
--    2025 baseline for comparison after the backfill (expect ~1.9k).
select count(distinct aths."AthleteId")       as athletes_2026_with_stats,
       count(distinct ass."AthleteSeasonId")  as roster_rows_2026_with_stats,
       count(distinct ass."Id")               as statistic_docs
from public."AthleteSeasonStatistic" ass
join public."AthleteSeason" aths on aths."Id" = ass."AthleteSeasonId"
join public."FranchiseSeason" fs on fs."Id" = aths."FranchiseSeasonId"
where fs."SeasonYear" = 2026;

select count(distinct aths."AthleteId")      as athletes_2025_with_stats,
       count(distinct ass."AthleteSeasonId") as roster_rows_2025_with_stats
from public."AthleteSeasonStatistic" ass
join public."AthleteSeason" aths on aths."Id" = ass."AthleteSeasonId"
join public."FranchiseSeason" fs on fs."Id" = aths."FranchiseSeasonId"
where fs."SeasonYear" = 2025;

-- athletes_2025_with_stats	roster_rows_2025_with_stats
-- 2330	2330

-- 2+3) The purge: guard + delete in ONE transaction. The guard RAISES if
--      any finalized 2026 contest exists, which aborts the transaction —
--      the deletes below then refuse to run and the COMMIT rolls back.
begin;

do $$
declare finalized_2026 int;
begin
    select count(*) into finalized_2026
    from public."Contest"
    where "SeasonYear" = 2026
      and "FinalizedUtc" is not null;

    if finalized_2026 > 0 then
        raise exception
            'PURGE ABORTED: % finalized 2026 contest(s) exist. A 2026 roster row can now legitimately carry stats — the blanket purge is unsafe. Use an identity-scoped variant instead.',
            finalized_2026;
    end if;
end $$;

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
-- VERIFICATION — run steps 4-5 twice:
--   (a) immediately after the purge: step 4 MUST be zero. Nonzero means a
--       concurrent writer raced the purge — re-run the script (idempotent)
--       and do NOT trigger the backfill until step 4 reads zero.
--   (b) again after the backfill batch completes in Seq: step 4 must STILL
--       be zero (the season guard must not attach anything to 2026 rows —
--       nonzero here = failed backfill, investigate before proceeding);
--       step 5's 2025 coverage should approach the active-roster count.
-- =============================================================================

-- 4) Remaining 2026-row stat docs — must be zero at both checkpoints.
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
where fs."SeasonYear" in (2011, 2022, 2023, 2024, 2025, 2026)
group by fs."SeasonYear"
order by fs."SeasonYear" desc;

-- @ 1555
-- SeasonYear	athletes_with_stats
-- 2025	12675
-- 2024	15909
-- 2023	16369
-- 2022	16493
-- 2011	10006

select fs."SeasonYear", count(ass."Id") as stat_docs
from public."AthleteSeason" aths
join public."FranchiseSeason" fs on fs."Id" = aths."FranchiseSeasonId"
left join public."AthleteSeasonStatistic" ass on ass."AthleteSeasonId" = aths."Id"
where aths."AthleteId" = 'ea6ec41d-31a1-623e-1a68-d21910f17bb8'
group by fs."SeasonYear"
order by fs."SeasonYear" desc;

-- SeasonYear	stat_docs
-- 2026	0
-- 2025	0
-- 2024	2
-- 2023	2
