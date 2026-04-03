namespace SportsData.Api.Tests.Smoke;

// Minimal response models for smoke test assertions.
// These intentionally only include fields we want to verify,
// not the full API contract.

public record PaginatedResponse<T>
{
    public List<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
}

public record VenueItem
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public string? Ref { get; init; }
    public Dictionary<string, string>? Links { get; init; }
}

public record FranchiseItem
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Slug { get; init; }
    public string? Abbreviation { get; init; }
    public string? Ref { get; init; }
    public Dictionary<string, string>? Links { get; init; }
}

public record FranchiseSeasonsResponse
{
    public Guid FranchiseId { get; init; }
    public string? FranchiseSlug { get; init; }
    public List<FranchiseSeasonItem> Items { get; init; } = [];
}

public record FranchiseSeasonItem
{
    public Guid Id { get; init; }
    public int SeasonYear { get; init; }
    public string? Slug { get; init; }
    public string? DisplayName { get; init; }
    public string? Abbreviation { get; init; }
    public string? Ref { get; init; }
}

public record ConferenceItem
{
    public string? Name { get; init; }
    public string? Slug { get; init; }
}
