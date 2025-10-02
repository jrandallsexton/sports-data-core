    SELECT fssc."Name" AS "Category",
        fss."Name" AS "StatisticKey",
        fss."Name" AS "StatisticValue",
        fss."DisplayValue",
        fss."PerGameValue",
        fss."PerGameDisplayValue",
        fss."Rank"
    from public."FranchiseSeasonStatisticCategory" fssc
    inner join public."FranchiseSeasonStatistic" fss on fss."FranchiseSeasonStatisticCategoryId" = fssc."Id"
    where fssc."FranchiseSeasonId" = @FranchiseSeasonId
    order by "Category", "StatisticKey"