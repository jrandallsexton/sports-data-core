namespace SportsData.Api.Application.Venues;

/// <summary>
/// External API response DTO for venue list.
/// Enriched with HATEOAS refs and navigation links.
/// </summary>
public class GetVenuesResponseDto
{
    public List<VenueResponseDto> Items { get; set; } = new();
    
    // Pagination metadata
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
    
    // HATEOAS links
    public Dictionary<string, Uri> Links { get; set; } = new();
}
