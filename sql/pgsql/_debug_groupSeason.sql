
select * from public."GroupSeason" where "SeasonYear" = 2026;

select * from public."FranchiseSeason" limit 100;

select Distinct "GroupSeasonMap" from public."FranchiseSeason" order by "GroupSeasonMap";

select * from public."FranchiseSeason" where "SeasonYear" = 2026;
select * from public."Contest" where "AwayTeamFranchiseSeasonId" = ''