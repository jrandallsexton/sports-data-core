select * from public."ContestPreview"
select * from public."PickemGroup"
select * from public."PickemGroupConference"
select * from public."PickemGroupWeek"
select * from public."PickemGroupMatchup"
select * from public."UserPick"
select * from public."User"
select *
from public."PickemGroupMatchup" m
where m."GroupId" = '96d4771f-43e5-470b-aba4-7401452ee1be' and "SeasonWeek" = 1
order by "Spread"
--delete from public."PickemGroupWeek"
--delete from public."PickemGroupMatchup"
--delete from public."UserPick"

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
