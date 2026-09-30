select * from public."MatchupPreview" order by "CreatedUtc" desc limit 10;

select * from public."Prompt" order by "CreatedUtc" desc limit 10;

select * from public."MatchupPreviewPrompt" order by "CreatedUtc" desc limit 10;

select * from public."MatchupPreview" where "ContestId" = '5aa78cf8-13ca-1996-d31d-51bb3bbeaf0b';

select * from public."MatchupPreviewPrompt" where "ContestId" = '5aa78cf8-13ca-1996-d31d-51bb3bbeaf0b';
select * from public."MatchupPreviewPrompt" where "PredictedSpreadWinnerId" is not null order by "CreatedUtc" desc;

select * from public."MatchupPreviewPrompt" where "PredictedStraightUpWinnerId" is not null;

select count(*) from public."MatchupPreviewPrompt" where "PredictedSpreadWinnerId" is null; -- 646
select count(*) from public."MatchupPreviewPrompt" where "PredictedStraightUpWinnerId" is null; -- 646