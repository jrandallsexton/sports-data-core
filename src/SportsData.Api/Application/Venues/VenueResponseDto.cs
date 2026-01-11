using SportsData.Core.Dtos.Canonical;

namespace SportsData.Api.Application.Venues;

/// <summary>
/// External API response DTO for venue resources.
/// Enriched with HATEOAS refs and navigation links.
/// </summary>
public class VenueResponseDto
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? ShortName { get; set; }

    public bool IsGrass { get; set; }

    public bool IsIndoor { get; set; }

    public required string Slug { get; set; }

    public int Capacity { get; set; }

    public List<VenueImageDto> Images { get; set; } = [];

    public AddressDto? Address { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    // HATEOAS additions
    public Uri? Ref { get; set; }

    public Dictionary<string, Uri> Links { get; set; } = new();
}
