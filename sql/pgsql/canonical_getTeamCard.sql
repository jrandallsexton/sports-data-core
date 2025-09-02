SELECT DISTINCT ON (F."Id")
	F."Slug" AS "Slug",
	F."DisplayName" AS "Name",
	F."DisplayNameShort" AS "ShortName",
	'-1' AS "Ranking",
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
	INNER JOIN PUBLIC."GroupSeason" GS ON GS."Id" = FS."GroupSeasonId"
	LEFT JOIN PUBLIC."FranchiseLogo" FL ON FL."FranchiseId" = F."Id"
	LEFT JOIN PUBLIC."Venue" V ON V."Id" = F."VenueId"
WHERE
	F."Slug" = 'lsu-tigers'
ORDER BY
    F."Id",
    FL."CreatedUtc" ASC NULLS LAST;

--	select * from public."GroupSeason" where "Slug" = 'sec';
--	select * from public."GroupSeason" where "Id" = '64210880-42d8-b5e3-9ed3-d3de2ed45617'