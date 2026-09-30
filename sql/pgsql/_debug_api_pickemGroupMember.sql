--SELECT * FROM public."User"

--select * from public."PickemGroupMember" where "UserId" = '5fa4c116-1993-4f2b-9729-c50c62150813';

-- Goal: Remove User0 from all pickem groups

select * from public."PickemGroup" order by "CreatedUtc" desc;

select * from public."PickemGroup" where "EndsOn" = '2026-09-08 06:59:00+00';
update public."PickemGroup" set "DeactivatedUtc" = '2026-09-08 06:59:00+00', "InvitationsExpireUtc" = '2026-09-08 06:59:00+00' where "Id" = '3863696c-0293-4241-9760-d11f6fecb9de';


select * from public."PickemGroupMatchup" order by "CreatedUtc" desc limit 10;
select * from public."PickemGroupMatchup" where "ContestId" = 'fa09dff7-f6fa-3788-0fc6-dfe1cc8c4984';

select * from public."PickemGroupMatchup" where "GroupId" = 'b3d8288d-a345-4ce1-9b0c-9268fb5f2bb8' and "SeasonWeek" = 1;
update public."PickemGroupMatchup"
set "AwayLosses" = 0, "AwayWins" = 0, "HomeLosses" = 0, "HomeWins" = 0
where "GroupId" = 'b3d8288d-a345-4ce1-9b0c-9268fb5f2bb8' and "SeasonWeek" = 1;

update "PickemGroupMatchup" set "AwayWins" = 1 where "ContestId" = 'fa09dff7-f6fa-3788-0fc6-dfe1cc8c4984';

select * from public."UserPick" where "UserId" = '5fa4c116-1993-4f2b-9729-c50c62150813'

select * from public."UserPick" where "ContestId" = '69e60763-d3e2-ad99-1655-ea38cf3ef2d9';

select * from public."UserPick" limit 10;