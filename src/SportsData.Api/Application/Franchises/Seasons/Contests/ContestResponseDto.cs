using SportsData.Core.Common;

namespace SportsData.Api.Application.Franchises.Seasons.Contests;

public record ContestResponseDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string ShortName { get; init; } = null!;
    public DateTime StartDateUtc { get; init; }
    public Sport Sport { get; init; }
    public int SeasonYear { get; init; }
    public int? Week { get; init; }
    
    // Home Team
    public Guid HomeTeamFranchiseSeasonId { get; init; }
    public string HomeTeamSlug { get; init; } = null!;
    public string HomeTeamDisplayName { get; init; } = null!;
    public int? HomeScore { get; init; }
    
    // Away Team
    public Guid AwayTeamFranchiseSeasonId { get; init; }
    public string AwayTeamSlug { get; init; } = null!;
    public string AwayTeamDisplayName { get; init; } = null!;
    public int? AwayScore { get; init; }
    
    // Status
    public bool IsFinal { get; init; }
    
    // HATEOAS
    public string Ref { get; init; } = null!;
    public ContestLinks Links { get; init; } = null!;
}

public record ContestLinks
{
    public string Self { get; init; } = null!;
    public string HomeTeam { get; init; } = null!;
    public string AwayTeam { get; init; } = null!;
    public string? Venue { get; init; }
}
