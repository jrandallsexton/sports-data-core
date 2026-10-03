-- Franchise seasons whose schedule looks incompletely sourced: fewer than
-- 10 contests (home or away) for the season.
-- Run against the sport's Producer DB (e.g. sdProducer.FootballNcaa).
--
-- Every division is included: FBS plays FCS (and lower), so an opponent's
-- incomplete sourcing matters regardless of division. The Division column
-- (derived from GroupSeasonMap the same way as the War Room's
-- conferenceParent) is there for sorting and triage only.
--
-- Repair: team page -> Admin tab -> "Source {season} season", then Enrich.

with fs as (
    select
        fs."Id",
        fs."Slug",
        fs."DisplayName",
        fs."GroupSeasonMap",
        case
            when fs."GroupSeasonMap" ilike '%fbs%'  then 'FBS'
            when fs."GroupSeasonMap" ilike '%fcs%'  then 'FCS'
            when fs."GroupSeasonMap" ilike '%diii%' then 'DIII'
            when fs."GroupSeasonMap" ilike '%dii%'  then 'DII'
            when fs."GroupSeasonMap" ilike '%naia%' then 'NAIA'
            else ''
        end as "Division"
    from public."FranchiseSeason" fs
    where fs."SeasonYear" = 2026
)
select
    fs."Division",
    fs."Slug",
    fs."DisplayName",
    count(distinct c."Id") as "Games",
    fs."Id" as "FranchiseSeasonId",
    fs."GroupSeasonMap"
from fs
left join public."Contest" c
    on c."HomeTeamFranchiseSeasonId" = fs."Id"
    or c."AwayTeamFranchiseSeasonId" = fs."Id"
group by fs."Division", fs."Slug", fs."DisplayName", fs."Id", fs."GroupSeasonMap"
having count(distinct c."Id") < 10
order by "Games", fs."Division", fs."Slug";
