select fr."Id" as "FranchiseId", f."Id" as "FranchiseSeasonId", f."Slug", f."Abbreviation", f."ColorCodeHex", f."ColorCodeAltHex"
from public."FranchiseSeason" f
inner join public."Franchise" fr on fr."Id" = f."FranchiseId"
where f."SeasonYear" = 2025 --and f."IsActive" = true
order by f."Slug";