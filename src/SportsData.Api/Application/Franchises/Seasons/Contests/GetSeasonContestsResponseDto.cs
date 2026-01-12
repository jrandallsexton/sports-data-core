namespace SportsData.Api.Application.Franchises.Seasons.Contests;

public record GetSeasonContestsResponseDto
{
    public List<ContestResponseDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public SeasonContestsFilters Filters { get; init; } = null!;
    public SeasonContestsLinks Links { get; init; } = null!;
}

public record SeasonContestsFilters
{
    public int SeasonYear { get; init; }
    public string FranchiseSlug { get; init; } = null!;
    public int? Week { get; init; }
}

public record SeasonContestsLinks
{
    public string Self { get; init; } = null!;
    public string Franchise { get; init; } = null!;
    public string Season { get; init; } = null!;
}
