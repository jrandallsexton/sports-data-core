select * from public."Prompt";

select * FROM public."MatchupPreview" order by "CreatedUtc" desc limit 25;

select * from public."MatchupPreview" where "RejectedUtc" is null and "ValidationErrors" is null order by "CreatedUtc" limit 25;

select count(DISTINCT "ContestId") from public."MatchupPreview" where "RejectedUtc" is null and "ValidationErrors" is null;