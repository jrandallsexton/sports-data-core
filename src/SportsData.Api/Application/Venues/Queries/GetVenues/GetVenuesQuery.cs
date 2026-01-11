namespace SportsData.Api.Application.Venues.Queries.GetVenues;

public class GetVenuesQuery
{
    public required string Sport { get; init; }
    public required string League { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
