
select * from public."MatchupPreview" where "ContestId" = '24477be2-e202-7ce2-ef3b-4b71a9bc3b58' and "RejectedUtc" is null order by "CreatedUtc" desc
-- http://localhost:5262/ui/matchup/8b0f8da2-4fa8-c797-73f1-000c14d82e1d/preview
--update "MatchupPreview" set "RejectedUtc" = '2025-09-10 13:39:08.011918+00' where "Id" = 'a97f5e56-6849-4087-8e80-d3d618048ec0'
update public."MatchupPreview" set
"PredictedStraightUpWinner" = '9130be89-0706-9aa1-927c-06fb75a303cd',
"PredictedSpreadWinner" = '9130be89-0706-9aa1-927c-06fb75a303cd'
where "Id" = 'c30dedee-663b-405f-8de5-ce7c5ea6998e'

/*
update public."MatchupPreview" set
"PredictedStraightUpWinner" = 'f3c03a13-7806-c144-9e0e-0d4910ac770d',
"PredictedSpreadWinner" = 'f3c03a13-7806-c144-9e0e-0d4910ac770d' WHERE "Id" = '2b4b3533-1b75-4e41-a4a5-1d62c0996545'
*/

select * from public."MatchupPreview" where "ContestId" = 'e2758932-b9d5-a450-fe56-5911e9756f43' order by "CreatedUtc" desc
select * from public."PickemGroup" where "IsPublic" = true
select * from public."UserPick" where "Week" = 11 and "UserId" = '5fa4c116-1993-4f2b-9729-c50c62150813' and "ContestId" = '24477be2-e202-7ce2-ef3b-4b71a9bc3b58'
--delete from public."UserPick" where "Week" = 11 and "UserId" = '5fa4c116-1993-4f2b-9729-c50c62150813' and "ContestId" = '24477be2-e202-7ce2-ef3b-4b71a9bc3b58'

-- FIX DEV - 02 NOV 2025
-- delete from public."UserPick" where "Week" = 10 and "UserId" = '5fa4c116-1993-4f2b-9729-c50c62150813';
-- update public."MatchupPreview" set "PredictedSpreadWinner" = 'd731cc7f-a671-e41e-5568-0b817ef911b2' where "Id" = 'aa6d0112-c543-4804-a1bf-165048bc79fb'; -- Memphis correction
-- update public."MatchupPreview" set "PredictedSpreadWinner" = 'a8dd1b64-3c64-d24c-30aa-10e01fa7cc66' where "Id" = '68f2771e-12ca-4179-9091-b1d8e5043afa'; -- NotreDame correction
-- update public."MatchupPreview" set "PredictedSpreadWinner" = 'c808465b-42c7-ddb7-9439-9f6e88a69b15', "RejectedUtc" = null  where "Id" = '125bc8ed-38c7-447c-9b04-5000269e1b08'; -- Michigan correction
-- update public."MatchupPreview" set "PredictedSpreadWinner" = '4dca16cb-464c-49f1-9b67-faff2e3d0b40' where "Id" = '866c0b89-a346-48d1-9f03-1da786632c00'; -- TN correction
-- update public."MatchupPreview" set "PredictedSpreadWinner" = '4dca16cb-464c-49f1-9b67-faff2e3d0b40' where "Id" = '3bb46636-cedf-4301-8794-2ff1af32b276'; -- TN correction
-- update public."MatchupPreview" set "PredictedSpreadWinner" = '366444ef-e37c-b0b1-816e-572e8cac7965' where "Id" = 'd8365bff-3ace-433d-b7fa-8057521eca81'; -- Utah
-- update public."MatchupPreview" set "PredictedSpreadWinner" = 'c3d0d4bc-3417-32e2-f579-9f71db71a9dd', "RejectedUtc" = null  where "Id" = 'e98ecaa6-9f46-4efd-9805-f5de6487d829'; -- Auburn correction
-- update public."UserPick" set "IsCorrect" = null, "PointsAwarded" = null, "WasAgainstSpread" = null, "ScoredAt" = null where "Week" = 10;

select g."Id", u."DisplayName" as "Commissioner", g."Name", g."Description", g."RankingFilter", g."PickType", g."UseConfidencePoints", g."DropLowWeeksCount"
from public."PickemGroup" g
inner join public."User" u on u."Id" = g."CommissionerUserId"
where g."IsPublic" = true

select * from public."PickemGroup"
select * from public."PickemGroupMember" where "PickemGroupId" = 'edf84c4b-04d0-488f-b18e-1fed96fb93c7'
select * from public."PickemGroupConference" where "PickemGroupId" = '1de3945f-4840-41d0-baba-dd371b157c31'
select * from public."PickemGroupWeek" where "GroupId" = 'aa7a482f-2204-429a-bb7c-75bc2dfef92b' order by "SeasonWeek"
select * from public."PickemGroupWeek" where "SeasonWeekId" = '66277eb1-12cd-37cc-eb5d-950f10468f6d'
--delete from public."PickemGroupWeek" where "SeasonWeekId" = '13343d64-6de5-3308-a484-14dc8c2dbcc6'
delete from public."PickemGroupMatchup" where "SeasonWeekId" = '13343d64-6de5-3308-a484-14dc8c2dbcc6'
-- https://api-dev.sportdeets.com/ui/matchup//preview
select *
from public."PickemGroupMatchup"
where
  --"GroupId" = '4319cb6e-e503-465f-8213-eacae5c0c948' and
  "SeasonWeekId" = '13343d64-6de5-3308-a484-14dc8c2dbcc6'
  --"ContestId" = '50a84a03-f14f-ef1f-52a3-67a59c6e583a'
order by "StartDateUtc" desc

-- FIX DEV - 07 OCT 2025
-- remove all scoring for week 6
-- update public."UserPick" set "IsCorrect" = null, "PointsAwarded" = null, "WasAgainstSpread" = null, "ScoredAt" = null where "Week" = 6

-- https://api-dev.sportdeets.com/ui/matchup/ef9045f3-0346-d153-baee-b97231badda1/preview

-- FIX DEV
-- delete from public."UserPick" where "CreatedUtc" > '2025-09-01 00:00:00.000000-04'
-- delete from public."PickemGroupMatchup" where "SeasonWeek" = 2
-- update public."PickemGroupWeek" set "AreMatchupsGenerated" = false where "SeasonWeek" = 2
-- delete from public."MatchupPreview" where "CreatedUtc" > '2025-09-01 00:00:00.000000-04'
-- in Producer, execute "FranchiseSeasonEnrichmentJob"
-- in Producer, execute "ContestUpdateJob"
-- in API, execute "MatchupScheduler"

select * from public."User"
select * from public."UserPick"
where
  "ContestId" = '8a64dddf-0094-9a3a-2618-55c276296ef8'
ORDER BY "CreatedUtc" desc

select * from public."ContestPrediction"
delete from public."ContestPrediction"
--update public."UserPick" set "PickType" = 2
--update public."UserPick" set "IsCorrect" = null, "PointsAwarded" = null, "WasAgainstSpread" = null, "ScoredAt" = null where "ContestId" = '8a64dddf-0094-9a3a-2618-55c276296ef8'

--delete from public."UserPick" where "UserId" = '5fa4c116-1993-4f2b-9729-c50c62150813' and "Week" = 3

--delete from public."PickemGroupMember" where "UserId" = '5fa4c116-1993-4f2b-9729-c50c62150813'
select * from public."UserPick" where "UserId" = '5fa4c116-1993-4f2b-9729-c50c62150813' and "Week" = 11
--delete from public."UserPick" where "UserId" = '5fa4c116-1993-4f2b-9729-c50c62150813'
--delete from public."UserPick" where "Week" = 3
--update public."UserPick" set "IsCorrect" = null, "PointsAwarded" = null, "WasAgainstSpread" = null, "ScoredAt" = null where "Week" = 2
select * from public."User"
--update public."User" set "DisplayName" = 'StatBot', "IsSynthetic" = true where "Id" = '5fa4c116-1993-4f2b-9729-c50c62150813'
--update public."User" set "DisplayName" = 'MetricBot', "IsSynthetic" = true where "Id" = 'b210d677-19c3-4f26-ac4b-b2cc7ad58c44'
select * from public."OutboxMessage"
select * from public."OutboxState"
select *
from public."PickemGroupMatchup" m
where m."GroupId" = 'aa7a482f-2204-429a-bb7c-75bc2dfef92b' and "SeasonWeek" = 2 --and "ContestId" = 'df5fe110-1801-c8c6-826c-847aa01f8e29'
order by "Spread"

-- SELECT *
-- FROM public."PickemGroupMatchup" m
-- WHERE m."GroupId" = 'aa7a482f-2204-429a-bb7c-75bc2dfef92b'
--   AND m."SeasonWeek" = 1
--   AND m."ContestId" IN (
--     SELECT "ContestId"
--     FROM public."PickemGroupMatchup"
--     WHERE "GroupId" = 'aa7a482f-2204-429a-bb7c-75bc2dfef92b'
--       AND "SeasonWeek" = 1
--     GROUP BY "ContestId"
--     HAVING COUNT(*) > 1
--   )


select * from public."MessageThread"
--update public."MessageThread" set "CreatedBy" = '11111111-1111-1111-1111-111111111111'
select * from public."MessagePost" where "ThreadId" = '00000000-0000-0000-0000-000000000000'
--update public."MessagePost" set "CreatedBy" = '11111111-1111-1111-1111-111111111111' where "Id" = '3359fc6b-3643-4f9b-a463-9bf51f2f25ae'
--delete from public."PickemGroupWeek"
--delete from public."PickemGroupMatchup"
--delete from public."UserPick"
--update public."User" set "DisplayName" = 'StatBot', "IsSynthetic" = true, "SignInProvider" = 'password' where "Id" = 'e972f550-acab-4162-a4ee-e76170a4c9e1'

/*
INSERT INTO "ContestPreview" (
    "Id",
    "ContestId",
    "Overview",
    "Analysis",
    "Prediction",
    "OverUnderPrediction",
    "CreatedUtc",
	"CreatedBy"
)
VALUES (
    '2f7c7383-1b3f-42e1-bf83-62b78b70dcd6', -- new unique ID for the preview
    '9eebea62-4eab-8722-ada7-6358ee5514f2', -- ContestId
    'South Carolina Gamecocks host Virginia Tech Hokies in a thrilling ACC matchup at Mercedes-Benz Stadium in Atlanta, Georgia. This early season clash will determine the momentum for both teams as they aim to secure a spot in the conference standings.',
    'The South Carolina Gamecocks boast a potent rushing attack, and their running back is one of the top performers in the SEC. The Hokies, on the other hand, feature a stingy defense that ranks among the best in the ACC. Virginia Tech''s offensive line will need to step up against South Carolina''s formidable defensive front.',
    'The Gamecocks will look to control the tempo with their powerful running game, while the Hokies aim to capitalize on turnovers and capitalize on their strong defense. Expect a close contest, but the South Carolina Gamecocks manage to secure a hard-fought victory, covering the spread of -7.5 points with a final score of 28-24.',
    0, -- OverUnderPrediction.None
    now(),
	'11111111-1111-1111-1111-111111111111'
);
{
  "overview": "",
  "analysis": "",
  "prediction": ""
}
*/




