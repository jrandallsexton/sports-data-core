select 
    sw."Id",
    sw."SeasonId",
    sw."Number" as "WeekNumber",
    s."Year" as "SeasonYear",
    sw."IsNonStandardWeek"
from public."SeasonWeek" sw
inner join public."Season" s on s."Id" = sw."SeasonId"
where s."Year" = @SeasonYear and sw."EndDate" < now()
order by sw."StartDate"