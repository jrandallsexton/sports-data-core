namespace SportsData.Producer.Application.Franchises.Queries.GetAllFranchises;

public record GetAllFranchisesQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    public const int MaxPageSize = 100;
    public const int MinPageSize = 1;
}
