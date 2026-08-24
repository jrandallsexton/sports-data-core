-- Preview historical-enrichment prototypes (companion to _debug_contest.sql)
-- Test franchises: Carolina 0b5dd1d6-4f0b-e546-02e2-cbdc5671d7ac
--                  Arizona  14f0ef58-8728-7b8f-cbed-a8e082539dc6

-- ============================================================
-- 1. Head-to-head, last 5 meetings (denormalized to what the model payload
--    needs: names/labels, no per-season GUIDs)
-- ============================================================
select
    c."StartDateUtc"::date                         as game_date,
    c."SeasonYear",
    sp."Name"                                      as season_phase,   -- Regular Season / Postseason
    fHome."DisplayName"                            as home_team,
    fAway."DisplayName"                            as away_team,
    c."HomeScore", c."AwayScore",
    case when c."WinnerFranchiseSeasonId" = c."HomeTeamFranchiseSeasonId"
         then fHome."DisplayName" else fAway."DisplayName" end as winner,
    case when c."SpreadWinnerFranchiseSeasonId" is null then null
         when c."SpreadWinnerFranchiseSeasonId" = c."HomeTeamFranchiseSeasonId"
         then fHome."DisplayName" else fAway."DisplayName" end as spread_winner,   -- null pre-~2012 + some rows
    case c."OverUnder" when 1 then 'Over' when 2 then 'Under' else null end as ou_result,  -- Contest.OverUnder is the RESULT enum, not the line
    c."EventNote"
from public."Contest" c
join public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
join public."Franchise" fAway        on fAway."Id" = fsAway."FranchiseId"
join public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
join public."Franchise" fHome        on fHome."Id" = fsHome."FranchiseId"
left join public."SeasonPhase" sp    on sp."Id" = c."SeasonPhaseId"
where ((fHome."Id" = '14f0ef58-8728-7b8f-cbed-a8e082539dc6' and fAway."Id" = '0b5dd1d6-4f0b-e546-02e2-cbdc5671d7ac')
    or (fHome."Id" = '0b5dd1d6-4f0b-e546-02e2-cbdc5671d7ac' and fAway."Id" = '14f0ef58-8728-7b8f-cbed-a8e082539dc6'))
  and c."FinalizedUtc" is not null
  and c."CancelledUtc" is null
order by c."StartDateUtc" desc
limit 5;

-- ============================================================
-- 2. Recency bridge: last 5 games of the PRIOR season for one franchise
--    (constant shape year-round; pair with current-season CompetitionResults)
-- ============================================================
select
    c."StartDateUtc"::date as game_date,
    c."SeasonYear",
    sp."Name"              as season_phase,
    fHome."DisplayName"    as home_team,
    fAway."DisplayName"    as away_team,
    c."HomeScore", c."AwayScore",
    case when c."WinnerFranchiseSeasonId" = c."HomeTeamFranchiseSeasonId"
         then fHome."DisplayName" else fAway."DisplayName" end as winner
from public."Contest" c
join public."FranchiseSeason" fsAway on fsAway."Id" = c."AwayTeamFranchiseSeasonId"
join public."Franchise" fAway        on fAway."Id" = fsAway."FranchiseId"
join public."FranchiseSeason" fsHome on fsHome."Id" = c."HomeTeamFranchiseSeasonId"
join public."Franchise" fHome        on fHome."Id" = fsHome."FranchiseId"
left join public."SeasonPhase" sp    on sp."Id" = c."SeasonPhaseId"
where (fHome."Id" = '0b5dd1d6-4f0b-e546-02e2-cbdc5671d7ac' or fAway."Id" = '0b5dd1d6-4f0b-e546-02e2-cbdc5671d7ac')
  and c."SeasonYear" = 2025          -- SeasonYear - 1 relative to the preview's season
  and c."FinalizedUtc" is not null
  and c."CancelledUtc" is null
order by c."StartDateUtc" desc
limit 5;

-- ============================================================
-- 3. Coverage check (doc sequencing step 2): were FranchiseSeasonMetric
--    season aggregates GENERATED for prior seasons?  Run per sport DB.
--    For NCAA, consider the FBS filter via GroupSeasonMap as in
--    _debug_franchiseSeason_metrics.sql - raw counts will understate
--    coverage if FCS franchises are included.
-- ============================================================
select
    fs."SeasonYear",
    count(distinct fs."Id")                  as franchise_seasons,
    count(distinct fsm."FranchiseSeasonId")  as with_metrics,
    round(100.0 * count(distinct fsm."FranchiseSeasonId")
        / nullif(count(distinct fs."Id"), 0), 1) as pct
from public."FranchiseSeason" fs
left join public."FranchiseSeasonMetric" fsm on fsm."FranchiseSeasonId" = fs."Id"
group by fs."SeasonYear"
order by fs."SeasonYear" desc;
