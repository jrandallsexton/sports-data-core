/* Game Information */
SELECT
  comp."Date" as "StartDateUtc",
  comp."Attendance",
      v."Name" as "Venue",
    v."City" as "VenueCity",
    v."State" as "VenueState",
    vi."Uri" as "VenueImageUri"
FROM public."Competition" comp
INNER JOIN public."Contest" c ON c."Id" = comp."ContestId"
inner join public."Venue" v on v."Id" = c."VenueId"
LEFT JOIN LATERAL (
  SELECT "Uri"
  FROM public."VenueImage"
  WHERE "VenueId" = v."Id"
  ORDER BY "CreatedUtc" ASC NULLS LAST
  LIMIT 1
) vi ON true
where c."Id" = @ContestId