namespace SportsData.Api.Application.Contests.Queries.GetContestById.Dtos;

public record ContestDetailResponseDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string ShortName { get; init; } = null!;
    public DateTime StartDateUtc { get; init; }
    public string Sport { get; init; } = null!;
    public int SeasonYear { get; init; }
    public int? Week { get; init; }
    
    public Guid HomeTeamFranchiseSeasonId { get; init; }
    public string HomeTeamSlug { get; init; } = null!;
    public string HomeTeamDisplayName { get; init; } = null!;
    public int? HomeScore { get; init; }
    
    public Guid AwayTeamFranchiseSeasonId { get; init; }
    public string AwayTeamSlug { get; init; } = null!;
    public string AwayTeamDisplayName { get; init; } = null!;
    public int? AwayScore { get; init; }
    
    public bool IsFinal { get; init; }
    
    public string Ref { get; init; } = null!;
    public ContestDetailLinks Links { get; init; } = null!;
}

public record ContestDetailLinks
{
    public string Self { get; init; } = null!;
    public string HomeTeam { get; init; } = null!;
    public string AwayTeam { get; init; } = null!;
    public string Venue { get; init; } = null!;
}
