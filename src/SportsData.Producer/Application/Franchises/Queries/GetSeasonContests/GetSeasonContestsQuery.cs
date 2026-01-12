namespace SportsData.Producer.Application.Franchises.Queries.GetSeasonContests;

public record GetSeasonContestsQuery(
    Guid FranchiseId,
    int SeasonYear,
    int? Week = null,
    int PageNumber = 1,
    int PageSize = 50
);
