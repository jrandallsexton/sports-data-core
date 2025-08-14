select fs."SeasonYear"
from public."FranchiseSeason" fs
inner join public."Franchise" f on f."Id" = fs."FranchiseId"
where f."Slug" = @Slug
order by fs."SeasonYear" DESC