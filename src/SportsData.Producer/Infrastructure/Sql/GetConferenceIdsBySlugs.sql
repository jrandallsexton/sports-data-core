SELECT "Id", "Slug"
FROM public."GroupSeason"
WHERE "Slug" = ANY(@Slugs) AND "SeasonYear" = @SeasonYear
