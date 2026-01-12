using System;
using System.Collections.Generic;
using SportsData.Core.Common;

namespace SportsData.Core.Infrastructure.Clients.Contest.Queries;

public record SeasonContestDto
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
    
    // Venue
    public Guid? VenueId { get; init; }
    
    // Status
    public DateTime? FinalizedUtc { get; init; }
    public bool IsFinal { get; init; }
    
    public DateTime CreatedUtc { get; init; }
}

public record GetSeasonContestsResponse(List<SeasonContestDto> Contests);
