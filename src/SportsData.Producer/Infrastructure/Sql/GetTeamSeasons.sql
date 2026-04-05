SELECT fs."SeasonYear"
FROM public."FranchiseSeason" fs
INNER JOIN public."Franchise" f ON f."Id" = fs."FranchiseId"
WHERE f."Slug" = @Slug
ORDER BY fs."SeasonYear" DESC
