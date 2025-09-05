select * from public."FranchiseSeason" where "Slug" = 'lsu-tigers'

select * from public."Contest"
where "HomeTeamFranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464' or "AwayTeamFranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464'
order by "StartDateUtc"

select * from public."Contest" where "Id" = 'd0e5466d-b719-5d32-c0cb-d2f44482bc0d';

select * from public."Competition" where "ContestId" = '8fac22f3-a8a4-773c-672b-d1c293f5d4a2';

select * from public."CompetitionOdds" where "CompetitionId" = 'f5cfd727-3b4a-f464-1ce1-8d2ffbc4e652';
select * from public."CompetitionOdds" where "Details" like '%LSU%'

select * from public."CompetitionTeamOdds" where "FranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464'