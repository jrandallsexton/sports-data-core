namespace SportsData.Producer.Application.Venues.Queries.GetAllVenues;

public record GetAllVenuesQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    // Optional: Add validation
    public const int MaxPageSize = 100;
    public const int MinPageSize = 1;
}
