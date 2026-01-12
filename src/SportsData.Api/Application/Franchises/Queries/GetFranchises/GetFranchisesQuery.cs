namespace SportsData.Api.Application.Franchises.Queries.GetFranchises;

public record GetFranchisesQuery(string Sport, string League, int PageNumber, int PageSize);
