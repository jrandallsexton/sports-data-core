SELECT fssc."Name" AS "Category",
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
