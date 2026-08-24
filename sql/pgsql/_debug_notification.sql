select * from public."NotificationLog" order by "CreatedUtc" desc;

select * from public."Users";
select * from public."UserDevices";

select * from public."UserPicks" order by "CreatedUtc" desc;

select * from public."PickemGroups" order by "CreatedUtc" desc;

select * from public."PickemGroupMatchups" where "PickemGroupId" = '164286f4-e574-41aa-bfa5-ed9d5a0f5ab8';

select * from public."UserNotificationPreferences";

select * from public."SmackPhrases";

select * from public."SmackPreviewRatings";