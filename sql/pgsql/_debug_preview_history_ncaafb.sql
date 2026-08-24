-- Preview historical-enrichment prototypes (companion to _debug_contest.sql)
-- Test franchises: san-jose-state-spartans d0b46660-0cb4-eeef-19d3-2152d36c5ad1
--                  usc-trojans  802901f4-754a-7646-8d21-c8f3a216804f
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
  where ((fHome."Id" = '802901f4-754a-7646-8d21-c8f3a216804f' and fAway."Id" = 'd0b46660-0cb4-eeef-19d3-2152d36c5ad1')
      or (fHome."Id" = 'd0b46660-0cb4-eeef-19d3-2152d36c5ad1' and fAway."Id" = '802901f4-754a-7646-8d21-c8f3a216804f'))
    and c."FinalizedUtc" is not null
    and c."CancelledUtc" is null
  order by c."StartDateUtc" desc
  limit 5;

-- game_date	SeasonYear	season_phase	home_team	away_team	HomeScore	AwayScore	winner	spread_winner	ou_result	EventNote
-- 2023-08-27	2023	Regular Season	USC Trojans	San José State Spartans	56	28	USC Trojans	San José State Spartans	Over	NULL
-- 2021-09-04	2021	Regular Season	USC Trojans	San José State Spartans	30	7	USC Trojans	USC Trojans	Under	NULL
-- 2009-09-05	2009	Regular Season	USC Trojans	San José State Spartans	56	3	USC Trojans	NULL	NULL	NULL
-- 2001-09-01	2001	Regular Season	USC Trojans	San José State Spartans	21	10	USC Trojans	NULL	NULL	NULL

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
where (fHome."Id" = '802901f4-754a-7646-8d21-c8f3a216804f' or fAway."Id" = '802901f4-754a-7646-8d21-c8f3a216804f')
  and c."SeasonYear" = 2025          -- SeasonYear - 1 relative to the preview's season
  and c."FinalizedUtc" is not null
  and c."CancelledUtc" is null
order by c."StartDateUtc" desc
limit 5;

-- game_date	SeasonYear	season_phase	home_team	away_team	HomeScore	AwayScore	winner
-- 2025-12-31	2025	Postseason	TCU Horned Frogs	USC Trojans	30	27	TCU Horned Frogs
-- 2025-11-30	2025	Regular Season	USC Trojans	UCLA Bruins	29	10	USC Trojans
-- 2025-11-22	2025	Regular Season	Oregon Ducks	USC Trojans	42	27	Oregon Ducks
-- 2025-11-15	2025	Regular Season	USC Trojans	Iowa Hawkeyes	26	21	USC Trojans
-- 2025-11-08	2025	Regular Season	USC Trojans	Northwestern Wildcats	38	17	USC Trojans