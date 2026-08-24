select con."Name", cs.*
from public."CompetitionStream" cs
inner join public."Competition" comp on comp."Id" = cs."CompetitionId"
inner join public."Contest" con on con."Id" = comp."ContestId"
order by cs."ScheduledTimeUtc" desc;