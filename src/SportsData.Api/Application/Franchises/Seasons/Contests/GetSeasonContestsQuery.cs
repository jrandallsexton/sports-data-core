namespace SportsData.Api.Application.Franchises.Seasons.Contests;

public record GetSeasonContestsQuery(
    string Sport,
    string League,
    string FranchiseId,
    int SeasonYear,
    int? Week = null,
    int PageNumber = 1,
    int PageSize = 50
);
