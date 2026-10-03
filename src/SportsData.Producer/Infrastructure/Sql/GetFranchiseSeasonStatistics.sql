SELECT fssc."Name" AS "Category",
    -- Human label for the category. "Category" stays the slug ("defensive"):
    -- it is the dictionary key the API's StatFormattingService and both UIs
    -- key on, and the per-device collapse key on mobile.
    fssc."ShortDisplayName" AS "CategoryDisplayName",
    fss."Name" AS "StatisticKey",
    fss."Name" AS "StatisticValue",
    fss."DisplayValue",
    fss."PerGameValue",
    fss."PerGameDisplayValue",
    fss."Rank"
FROM public."FranchiseSeasonStatisticCategory" fssc
INNER JOIN public."FranchiseSeasonStatistic" fss ON fss."FranchiseSeasonStatisticCategoryId" = fssc."Id"
WHERE fssc."FranchiseSeasonId" = @FranchiseSeasonId
ORDER BY "Category", "StatisticKey"
