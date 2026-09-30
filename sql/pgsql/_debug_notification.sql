select * from public."NotificationLog" order by "CreatedUtc" desc;

select * from public."Users" order by "CreatedUtc" desc;

select * from public."UserDevices";

select * from public."UserPicks" order by "CreatedUtc" desc;

select * from public."PickemGroups" where "Sport" = 2 order by "CreatedUtc" desc;

 SELECT COUNT(*) FROM public."PickemGroupMembers";

   SELECT "PickemGroupId", COUNT(*) FROM public."PickemGroupMembers"
  WHERE "PickemGroupId" IN ('b3d8288d-a345-4ce1-9b0c-9268fb5f2bb8','5ab3e9dc-b8e2-45b8-b217-d09bdacdf30f')
  GROUP BY 1;

select count(*) from public."PickemGroupMatchups"

select * from public."PickemGroupMatchups" where "PickemGroupId" = '164286f4-e574-41aa-bfa5-ed9d5a0f5ab8';

select * from public."UserNotificationPreferences";

select u."DisplayName", ud."FcmToken", ud."Platform", ud."NotificationsEnabled", unp."PickResultEnabled", unp."PickDeadlineReminderEnabled",
unp."ContestStartReminderEnabled", unp."LeagueInviteEnabled", unp."MembershipEnabled",
unp."MatchupPreviewEnabled", unp."ScheduleChangeEnabled", unp."OddsChangedEnabled",
unp."MatchupsReadyEnabled", unp."PollReleasedEnabled"
from public."Users" u
inner join public."UserNotificationPreferences" unp on u."Id" = unp."UserId"
inner join public."UserDevices" ud on ud."UserId" = u."Id";

select * from public."SmackPhrases";

select * from public."SmackPreviewRatings";

SELECT COUNT(*) FROM public."PendingScheduledJobs";

  SELECT "WaveAnchorUtc", "ScheduledFireUtc", COUNT(*)
  FROM public."PendingScheduledJobs"
  WHERE "JobKind" = 'PickDeadline'
  GROUP BY 1, 2 ORDER BY 1;

  select * from "NotificationPollReleases";