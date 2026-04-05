SELECT DISTINCT
    gsParent."Name" as "Division",
    gs."ShortName",
    gs."Slug"
FROM public."GroupSeason" gs
INNER JOIN public."GroupSeason" gsParent
    ON gsParent."Id" = gs."ParentId"
WHERE gs."IsConference" = true
  AND gs."SeasonYear" = @SeasonYear
ORDER BY gs."ShortName"
