select * from public."FranchiseSeason" where "Slug" = 'lsu-tigers'

select * from public."Contest"
where "HomeTeamFranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464' or "AwayTeamFranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464'
order by "StartDateUtc"

select * from public."Contest" where "Id" = '7f39067b-40bb-aa0b-225d-7670409d1003';

select * from public."Competition" where "ContestId" = '7f39067b-40bb-aa0b-225d-7670409d1003';

select * from public."CompetitionOdds" where "CompetitionId" = 'eda0c287-0d48-4715-4405-51414c3a416b';
select * from public."CompetitionOdds" where "Details" like '%LSU%'

select * from public."CompetitionTeamOdds" where "FranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464'