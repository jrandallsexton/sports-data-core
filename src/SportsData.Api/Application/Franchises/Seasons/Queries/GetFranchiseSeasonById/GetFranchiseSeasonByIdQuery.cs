namespace SportsData.Api.Application.Franchises.Seasons.Queries.GetFranchiseSeasonById;

public record GetFranchiseSeasonByIdQuery(
    string Sport,
    string League,
    string FranchiseSlugOrId,
    int SeasonYear);
