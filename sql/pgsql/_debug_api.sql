select * from public."ContestPreview" where "ContestId" = '0e27c391-408c-90af-c810-cd006ffbc10e'
select * from public."MatchupPreview" order by "CreatedUtc" desc
select * from public."PickemGroup"

select * from public."PickemGroupMember" where "PickemGroupId" = 'edf84c4b-04d0-488f-b18e-1fed96fb93c7'
select * from public."PickemGroupConference" where "PickemGroupId" = '1de3945f-4840-41d0-baba-dd371b157c31'
select * from public."PickemGroupWeek" where "GroupId" = '620d8af8-cf6f-49de-9a66-658bbfd02e82'
select * from public."PickemGroupMatchup" where "GroupId" = '620d8af8-cf6f-49de-9a66-658bbfd02e82'
select * from public."UserPick"
select * from public."User"
select * from public."OutboxMessage"
select * from public."OutboxState"
select *
from public."PickemGroupMatchup" m
where m."GroupId" = '96d4771f-43e5-470b-aba4-7401452ee1be' and "SeasonWeek" = 1
order by "Spread"

select * from public."MessageThread"
select * from public."MessagePost" where "ThreadId" = '00000000-0000-0000-0000-000000000000'
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




