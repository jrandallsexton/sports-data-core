select * FROM public."MatchupPreview" where "ContestId" = '538ec1c5-35f9-3db4-fe39-cdaf8ba58bc9';

select * from public."MatchupPreview" where "RejectedUtc" is null and "ValidationErrors" is null order by "CreatedUtc" limit 25;

select count(DISTINCT "ContestId") from public."MatchupPreview" where "RejectedUtc" is null and "ValidationErrors" is null;