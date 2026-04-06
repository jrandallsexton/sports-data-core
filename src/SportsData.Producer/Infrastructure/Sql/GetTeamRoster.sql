SELECT
    asl."Id" AS "AthleteSeasonId",
    asl."DisplayName",
    asl."ShortName",
    asl."Slug",
    asl."Jersey",
    ap."DisplayName" AS "Position",
    ap."Abbreviation" AS "PositionAbbreviation",
    asl."HeightDisplay",
    asl."WeightDisplay",
    asl."ExperienceDisplayValue",
    asl."ExperienceYears",
    asl."IsActive"
FROM public."AthleteSeason" asl
INNER JOIN public."AthletePosition" ap ON ap."Id" = asl."PositionId"
INNER JOIN public."FranchiseSeason" fs ON fs."Id" = asl."FranchiseSeasonId"
INNER JOIN public."Franchise" f ON f."Id" = fs."FranchiseId"
WHERE f."Slug" = @Slug AND fs."SeasonYear" = @SeasonYear
ORDER BY ap."Name", asl."LastName", asl."FirstName"
