select * from public."PickemGroup" order by "CreatedUtc" desc;

--update public."PickemGroup" set "DeactivatedUtc" = NOW() where "Id" = '4319cb6e-e503-465f-8213-eacae5c0c948';
--update public."PickemGroup" set "DeactivatedUtc" = NOW() where "CreatedUtc" < '2025-09-01 00:15:50.451784+00';

select * from public."PickemGroup" where "Id" = '64ac64df-6bdc-436a-a344-71b98dd2b685';

select * from public."PickemGroupInvitations";

select * from public."PickemGroupWeek" where "GroupId" = 'aff098a8-ed68-4070-9302-a6620ca183d6';
select * from public."PickemGroupMatchup" where "GroupId" = 'f4797807-0777-463b-adc4-8c0cda3d5290';

select * from public."UserPick" WHERE "PickemGroupId" = 'f4797807-0777-463b-adc4-8c0cda3d5290'; -- and "ContestId" = 'cae13b4e-89a9-9433-21a3-e1babde56628';

select * from public."User" order by "DisplayName"