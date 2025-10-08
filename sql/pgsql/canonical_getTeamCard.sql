WITH next_week AS (
  SELECT sw."Id" AS "SeasonWeekId",
         sw."Number" AS "WeekNumber",
         s."Id" AS "SeasonId",
         s."Year" AS "SeasonYear"
  FROM public."Season" s
  JOIN public."SeasonWeek" sw ON sw."SeasonId" = s."Id"
  JOIN public."SeasonPhase" sp ON sp."Id" = sw."SeasonPhaseId"
  WHERE sp."Name" = 'Regular Season'
    AND sw."StartDate" <= CURRENT_DATE and sw."EndDate" > CURRENT_DATE
  ORDER BY sw."StartDate"
  LIMIT 1
)

SELECT DISTINCT ON (F."Id")
	FS."Id" AS "FranchiseSeasonId",
	F."Slug" AS "Slug",
	F."DisplayName" AS "Name",
	F."DisplayNameShort" AS "ShortName",
	fsrd."Current" AS "Ranking",
	GS."Name" AS "ConferenceName",
	GS."ShortName" AS "ConferenceShortName",
	GS."Slug" AS "ConferenceSlug",
	FS."Wins" || '-' || FS."Losses" || '-' || FS."Ties" AS "OverallRecord",
	FS."ConferenceWins" || '-' || FS."ConferenceLosses" || '-' || FS."ConferenceTies" AS "ConferenceRecord",
	F."ColorCodeHex" AS "ColorPrimary",
	F."ColorCodeAltHex" AS "ColorSecondary",
	FL."Uri" AS "LogoUrl",
	NULL AS "HelmetUrl",
	F."Location" AS "Location",
	V."Name" AS "StadiumName",
	V."Capacity" AS "StadiumCapacity"
FROM
	PUBLIC."Franchise" F
	INNER JOIN PUBLIC."FranchiseSeason" FS on FS."FranchiseId" = F."Id"
	left  join public."FranchiseSeasonRanking" fsr on fsr."FranchiseSeasonId" = FS."Id" and fsr."Type" = 'ap' and fsr."SeasonWeekId" = (select "SeasonWeekId" from next_week)
	left  join public."FranchiseSeasonRankingDetail" fsrd on fsrd."FranchiseSeasonRankingId" = fsr."Id"
	INNER JOIN PUBLIC."GroupSeason" GS ON GS."Id" = FS."GroupSeasonId"
	LEFT JOIN PUBLIC."FranchiseLogo" FL ON FL."FranchiseId" = F."Id"
	LEFT JOIN PUBLIC."Venue" V ON V."Id" = F."VenueId"
WHERE
	F."Slug" = 'lsu-tigers' and FS."SeasonYear" = 2025
ORDER BY
    F."Id",
    FL."CreatedUtc" ASC NULLS LAST;

--	select * from public."GroupSeason" where "Slug" = 'sec';
--	select * from public."GroupSeason" where "Id" = '64210880-42d8-b5e3-9ed3-d3de2ed45617'
--select * from PUBLIC."FranchiseSeasonRanking"