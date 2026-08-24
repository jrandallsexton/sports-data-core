-- Spread-context / ATS-result digging (companion to docs/features/matchup-spread-context.md)
-- Origin: 2026-08-20 — "Florida covered 0 of 4 as a 21+ favorite" vs "six 26.5+ wins"
-- turned out to be BOTH true (different filters), but exposed a real gap:
--
--   FINDING: 290 finalized 2025 games have a spread VALUE (providers 58/100)
--   and a final score but NULL Contest."SpreadWinnerFranchiseSeasonId" — their
--   CompetitionOdds rows were never enriched (FinalizedUtc NULL). Root cause:
--   the odds-late finalization path shipped 2026-06-22 (#447), months after
--   the 2025 season; during live 2025, odds landing after finalization were
--   never picked up. 2022-2024 (historical backfills) are clean.
--   Every one of these silently deflates ATS-bucket facts for 2025 games
--   (e.g. Florida's true 21+ record is 1-of-5, not 0-of-4).
--
--   REMEDIATION PATH: POST {producer}/contests/{contestId}/admin/reenrich
--   per contest (query #6 lists them). CAUTION: reenrich fires
--   ContestFinalized -> API re-enqueues ScorePicksCommand, and the ATS winner
--   changes null -> value; if any 2025 ATS leagues scored against these
--   contests, historical pick scoring may change.
--
-- Test franchises: florida-gators   6237f249-8bad-6eaa-7363-961106eae073
--                  fau-owls         cc92c8f2-651d-5948-9736-fb6bf52c8e9f
-- Test contest:    FAU @ FLA 2026-09-05  dbebd45e-16c4-1b6a-52e2-5f1be188afc2 (FLA -26.5)
-- Odds providers:  58 = EspnBet (preferred), 100 = DraftKings (fallback)

-- ============================================================
-- 1. Franchise id lookup
-- ============================================================
select "Id", "Name", "DisplayName" from public."Franchise"
where "DisplayName" ilike '%florida%';

-- ============================================================
-- 2. A contest + its current line (matches GetContestSpreadTarget.sql)
-- ============================================================
select c."Id", c."StartDateUtc"::date, fA."DisplayName" as away, fH."DisplayName" as home,
       co."Details", co."Spread"  -- home-relative; negative = home favored
from public."Contest" c
join public."FranchiseSeason" fsA on fsA."Id" = c."AwayTeamFranchiseSeasonId"
join public."Franchise" fA on fA."Id" = fsA."FranchiseId"
join public."FranchiseSeason" fsH on fsH."Id" = c."HomeTeamFranchiseSeasonId"
join public."Franchise" fH on fH."Id" = fsH."FranchiseId"
join public."Competition" comp on comp."ContestId" = c."Id"
left join public."CompetitionOdds" co on co."CompetitionId" = comp."Id" and co."ProviderId" in ('58','100')
where c."Id" = 'dbebd45e-16c4-1b6a-52e2-5f1be188afc2';

-- ============================================================
-- 3. Margin facts, raw: one franchise's wins (or losses) by >= X, with lines.
--    Mirrors GetFranchiseMarginFact.sql minus the latest-only collapse, so you
--    can see every qualifying game. Flip the score comparison for losses.
-- ============================================================
select c."StartDateUtc"::date, fA."DisplayName" as away, c."AwayScore",
       fH."DisplayName" as home, c."HomeScore", co."Details" as line,
       case when c."SpreadWinnerFranchiseSeasonId" is null then null
            when (fH."Id" = '6237f249-8bad-6eaa-7363-961106eae073' and c."SpreadWinnerFranchiseSeasonId" = c."HomeTeamFranchiseSeasonId")
              or (fA."Id" = '6237f249-8bad-6eaa-7363-961106eae073' and c."SpreadWinnerFranchiseSeasonId" = c."AwayTeamFranchiseSeasonId")
            then 'covered' else 'failed' end as ats
from public."Contest" c
join public."FranchiseSeason" fsA on fsA."Id" = c."AwayTeamFranchiseSeasonId"
join public."Franchise" fA on fA."Id" = fsA."FranchiseId"
join public."FranchiseSeason" fsH on fsH."Id" = c."HomeTeamFranchiseSeasonId"
join public."Franchise" fH on fH."Id" = fsH."FranchiseId"
join public."Competition" comp on comp."ContestId" = c."Id"
left join public."CompetitionOdds" co on co."CompetitionId" = comp."Id" and co."ProviderId" in ('58','100') and co."Spread" is not null
left join public."SeasonPhase" sp on sp."Id" = c."SeasonPhaseId"
where c."FinalizedUtc" is not null and c."CancelledUtc" is null
  and (sp."TypeCode" is null or sp."TypeCode" <> 1)          -- preseason excluded
  and c."SeasonYear" >= 2022                                  -- window of interest
  and ((fH."Id" = '6237f249-8bad-6eaa-7363-961106eae073' and c."HomeScore" - c."AwayScore" >= 26.5)   -- wins by X (home)
    or (fA."Id" = '6237f249-8bad-6eaa-7363-961106eae073' and c."AwayScore" - c."HomeScore" >= 26.5))  -- wins by X (away)
order by c."StartDateUtc" desc;

-- ============================================================
-- 4. ATS bucket, raw: every game the franchise was a >= X favorite (or dog),
--    with per-game covered/failed. Mirrors GetFranchiseAtsBucket.sql.
--    Favorite: home side Spread <= -X, away side Spread >= X. Swap for dog.
-- ============================================================
select c."StartDateUtc"::date, fA."DisplayName" as away, c."AwayScore",
       fH."DisplayName" as home, c."HomeScore", co."Details" as line,
       case when (fH."Id" = '6237f249-8bad-6eaa-7363-961106eae073' and c."SpreadWinnerFranchiseSeasonId" = c."HomeTeamFranchiseSeasonId")
              or (fA."Id" = '6237f249-8bad-6eaa-7363-961106eae073' and c."SpreadWinnerFranchiseSeasonId" = c."AwayTeamFranchiseSeasonId")
            then 'covered' else 'failed' end as ats
from public."Contest" c
join public."FranchiseSeason" fsA on fsA."Id" = c."AwayTeamFranchiseSeasonId"
join public."Franchise" fA on fA."Id" = fsA."FranchiseId"
join public."FranchiseSeason" fsH on fsH."Id" = c."HomeTeamFranchiseSeasonId"
join public."Franchise" fH on fH."Id" = fsH."FranchiseId"
join public."Competition" comp on comp."ContestId" = c."Id"
join public."CompetitionOdds" co on co."CompetitionId" = comp."Id" and co."ProviderId" in ('58','100') and co."Spread" is not null
left join public."SeasonPhase" sp on sp."Id" = c."SeasonPhaseId"
where c."FinalizedUtc" is not null and c."CancelledUtc" is null
  and (sp."TypeCode" is null or sp."TypeCode" <> 1)
  and c."SpreadWinnerFranchiseSeasonId" is not null          -- decided ATS results only (THE GAP HIDES HERE)
  and ((fH."Id" = '6237f249-8bad-6eaa-7363-961106eae073' and co."Spread" <= -21)
    or (fA."Id" = '6237f249-8bad-6eaa-7363-961106eae073' and co."Spread" >= 21))
order by c."StartDateUtc" desc;

-- ============================================================
-- 5. THE GAP by season: finalized + scored + spread VALUE present, but no
--    Contest-level ATS result (true pushes excluded). 2025 = 290 as of
--    2026-08-20; every other season = 0. After remediation this should be 0.
-- ============================================================
select c."SeasonYear",
       count(*) as gap_games,
       count(*) filter (where co."FinalizedUtc" is null) as odds_never_enriched
from public."Contest" c
join public."Competition" comp on comp."ContestId" = c."Id"
join public."CompetitionOdds" co on co."CompetitionId" = comp."Id" and co."ProviderId" in ('58','100') and co."Spread" is not null
where c."FinalizedUtc" is not null and c."CancelledUtc" is null
  and c."HomeScore" is not null and c."AwayScore" is not null
  and c."SpreadWinnerFranchiseSeasonId" is null
  and abs((c."HomeScore" - c."AwayScore") + co."Spread") > 0.01
group by 1 order by 1;

-- ============================================================
-- 6. The gap CONTESTS, listed for replay via
--    POST {producer}/contests/{contestId}/admin/reenrich
-- ============================================================
select c."Id" as contest_id, c."StartDateUtc"::date, c."Name",
       co."Details" as line, c."AwayScore", c."HomeScore"
from public."Contest" c
join public."Competition" comp on comp."ContestId" = c."Id"
join public."CompetitionOdds" co on co."CompetitionId" = comp."Id" and co."ProviderId" in ('58','100') and co."Spread" is not null
where c."FinalizedUtc" is not null and c."CancelledUtc" is null
  and c."HomeScore" is not null and c."AwayScore" is not null
  and c."SpreadWinnerFranchiseSeasonId" is null
  and abs((c."HomeScore" - c."AwayScore") + co."Spread") > 0.01
order by c."StartDateUtc";

-- ============================================================
-- 7. Week-1 2026 canary: run #5 after the opening weekend finalizes.
--    Any 2026 rows = the odds-late path (#447) is not being triggered in
--    live flow and the hole is reopening.
-- ============================================================

-- ============================================================
-- 8. Remediation list: the gap contests involving at least one FBS side —
--    the only ones feeding FBS matchup dialogs / preview payloads.
--    (29 rows as of 2026-08-20; 2 FBS-vs-FBS, 27 FBS-vs-nonFBS.)
--    Replay each via the API ops proxy:
--      POST {api}/admin/ops/producer/football/ncaa/contests/{contest_id}/admin/reenrich
--      header: X-Admin-Token
--    CAUTION (verified locally 2026-08-20): 49 already-scored ATS picks in 5
--    leagues (AP25_SEC_ATS, FOO_10_ACC, AP10_CUSA, AP10_SEC_ATS, AP20_SEC_ATS)
--    reference these contests; reenrich fires ContestFinalized -> pick
--    re-scoring, and results scored under a NULL spread winner may flip.
-- ============================================================
with gap as (
  select c."Id", c."Name", c."StartDateUtc", c."HomeTeamFranchiseSeasonId" hfs, c."AwayTeamFranchiseSeasonId" afs
  from public."Contest" c
  join public."Competition" comp on comp."ContestId" = c."Id"
  join public."CompetitionOdds" co on co."CompetitionId" = comp."Id" and co."ProviderId" in ('58','100') and co."Spread" is not null
  where c."FinalizedUtc" is not null and c."CancelledUtc" is null
    and c."HomeScore" is not null and c."AwayScore" is not null
    and c."SpreadWinnerFranchiseSeasonId" is null
    and abs((c."HomeScore" - c."AwayScore") + co."Spread") > 0.01
)
select gap."Id" as contest_id, gap."StartDateUtc"::date as game_date, gap."Name"
from gap
join public."FranchiseSeason" fsH on fsH."Id" = gap.hfs
join public."FranchiseSeason" fsA on fsA."Id" = gap.afs
where fsH."GroupSeasonMap" like '%fbs%' or fsA."GroupSeasonMap" like '%fbs%'
order by gap."StartDateUtc";

-- ============================================================
-- 9. Pick exposure for the query-8 contests (run against sdApi.All):
--    which leagues scored picks on them, and how many.
-- ============================================================
-- select pg."Name" as league, pg."PickType", count(up."Id") as picks,
--        count(up."Id") filter (where up."IsCorrect" is not null) as scored
-- from public."UserPick" up
-- join public."PickemGroup" pg on pg."Id" = up."PickemGroupId"
-- where up."ContestId" in ( /* ids from query 8 */ )
-- group by 1, 2;

-- sql/pgsql/_debug_spread_context_ats_gap.sql (untracked, your convention) — now with:
--   - Query #8: the 29 FBS-involving gap contests (2 FBS-vs-FBS, 27 FBS-vs-FCS), with the full caution note baked into the comments.
-- ──-─Query #9: the pick-exposure check to run against sdApi.All (commented─template─—─paste─the─ids─from─#8).────────────────────────────────────────────────────

--   bruno/ (new untracked─folder─at─repo─root─—─move─the─files─into─your─real─Bruno─collection─wherever─it─lives):────────────────────────────────────────────────
--   - reenrich-contest.bru — POST {{baseUrl}}/admin/ops/producer/football/ncaa/contests/{{contestId}}/admin/reenrich with the X-Admin-Token: {{adminToken}}
--   header. The docs block in the file covers vars and both batch options.
--   - reenrich-contests.csv — 29 rows with contestId,gameDate,name headers. In the Bruno app: Collection Runner → select the request → attach this as the data
--   file (the contestId column feeds the var per iteration). CLI equivalent: bru run reenrich-contest.bru --env <env> --csv-file-path reenrich-contests.csv.

--   Answer to your proxy question: yes — contests is already on AdminOpsProxyController's Producer allowlist (the allowlist is prefix-per-family, and
--   contests/{id}/admin/reenrich falls under it), so this is pure pass-through with zero code changes. Admin token in the X-Admin-Token header, sport/league in
--   the path route to the NCAA Producer pod.

--   Before you fire the batch, the one decision: those 49 already-scored ATS picks in your five 2025 test leagues (AP25_SEC_ATS, FOO_10_ACC, AP10_CUSA,
--   AP10_SEC_ATS, AP20_SEC_ATS) will re-score, and results recorded under a null spread winner may flip — historical standings in those leagues could shift. Note
--   that count came from your local copy of the API DB; if it's stale, prod exposure could differ slightly — query #9 against prod tells you exactly. Since
--   they're test leagues, probably a shrug, but it's your shrug to make. Note the run is synchronous per contest — 29 sequential calls should take well under a
--   minute.
