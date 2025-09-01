SELECT DISTINCT ON (F."Id")
	F."Slug" AS "Slug",
	F."DisplayName" AS "Name",
	F."DisplayNameShort" AS "ShortName",
	'-1' AS "Ranking",
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
	LEFT JOIN PUBLIC."FranchiseLogo" FL ON FL."FranchiseId" = F."Id"
	LEFT JOIN PUBLIC."Venue" V ON V."Id" = F."VenueId"
WHERE
	F."Slug" = 'lsu-tigers'
ORDER BY
    F."Id",
    FL."CreatedUtc" ASC NULLS LAST;