select * from public."Season" Where "Year" = 1998;

select * from public."SeasonPhase" where "Year" = 1998 order by "Slug";

select * from public."SeasonWeek" sw
inner join public."SeasonPhase" sp on sw."SeasonPhaseId" = sp."Id"
--where sp."Year" = 1998
order by sp."Year", sp."TypeCode", sw."Number";

select * from public."Contest" c
inner join public."SeasonWeek" sw on c."SeasonWeekId" = sw."Id"
inner join public."SeasonPhase" sp on sw."SeasonPhaseId" = sp."Id"
where sp."Year" = 1998 order by c."StartDateUtc";

select * from public."Competition" where date_part('year', "Date") = 1998 order by "Date";

select * from public."FranchiseSeason" where "SeasonYear" = 1998 order by "Slug";