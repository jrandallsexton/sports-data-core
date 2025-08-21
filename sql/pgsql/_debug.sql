
select * from public."FranchiseLogo" where "FranchiseId" = 'd2ca25ce-337e-1913-b405-69a16329efe7'
select * from public."FranchiseSeason" where "Slug" = 'lsu-tigers'
select * from public."AthletePosition"
/* Season Roster */
select
	ats."LastName", ats."FirstName", ats."DisplayName", ats."ShortName",
	ats."Slug", ap."Name" as "Position",
	ats."WeightLb", ats."WeightDisplay",
	ats."HeightIn", ats."HeightDisplay",
	ats."Jersey"
from public."AthleteSeason" ats
inner join public."AthletePosition" ap on ap."Id" = ats."PositionId"
where ats."FranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464' and
      ats."FirstName" != '-' and
	  ats."IsActive" = true
order by ap."Name", ats."LastName", ats."FirstName"

select * from public."FranchiseSeasonRecord"
select * from public."FranchiseSeason" where "FranchiseId" = 'd2ca25ce-337e-1913-b405-69a16329efe7'
select * from public."GroupSeason" where "Id" = '7ff6fb28-dd1a-dd42-28d1-45a4a4bda516'
select * from public."FranchiseSeasonRanking" where "FranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464'
select * from public."Season"
select * from public."SeasonPhase"
select * from public."SeasonWeek"
select * from public."Contest"
select * from public."Venue"

select *
from public."GroupSeason" gs
inner join public."Season" s on s."Id" = gs."SeasonId"
where gs."Slug" = 'sec' and gs."SeasonYear" = 2025