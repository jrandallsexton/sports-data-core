SELECT fssc."Name" AS "Category",
    fss."Name" AS "Statistic",
    fss."DisplayValue",
    fss."PerGameValue",
    fss."PerGameDisplayValue",
    fss."Rank"
FROM public."FranchiseSeasonStatisticCategory" fssc
INNER JOIN public."FranchiseSeasonStatistic" fss ON fss."FranchiseSeasonStatisticCategoryId" = fssc."Id"
WHERE fssc."FranchiseSeasonId" = @FranchiseSeasonId
ORDER BY "Category", "Statistic"
