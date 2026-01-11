namespace SportsData.Api.Application.Venues.Queries.GetVenueById;

public class GetVenueByIdQuery
{
    public required string Sport { get; init; }
    public required string League { get; init; }
    public required string Id { get; init; }
}
