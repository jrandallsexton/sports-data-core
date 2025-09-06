
SELECT json_agg(row_to_json(t))
FROM (
    SELECT fssc."Name" AS "Category",
        fss."Name" AS "Statistic",
        fss."DisplayValue",
        fss."PerGameValue",
        fss."PerGameDisplayValue",
        fss."Rank"
    from public."FranchiseSeasonStatisticCategory" fssc
    inner join public."FranchiseSeasonStatistic" fss on fss."FranchiseSeasonStatisticCategoryId" = fssc."Id"
    where fssc."FranchiseSeasonId" = 'c13b7c74-6892-3efa-2492-36ebf5220464'
    order by "Category", "Statistic"
) t;