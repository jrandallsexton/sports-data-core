namespace SportsData.Api.Application.UI.Picks.Dtos;

public class PickemMatchupDto
{
    public Guid ContestId { get; set; }
    public int SeasonYear { get; set; }

    public DateTime StartDateUtc { get; set; }
    public string? Venue { get; set; }
    public string? VenueCity { get; set; }
    public string? VenueState { get; set; }

    // Away Team
    public string Away { get; set; } = default!;
    public string AwaySlug { get; set; } = default!;
    public string? AwayLogoUri { get; set; }
    public int? AwayRank { get; set; }
    public int AwayWins { get; set; }
    public int AwayLosses { get; set; }
    public int AwayConferenceWins { get; set; }
    public int AwayConferenceLosses { get; set; }

    // Home Team
    public string Home { get; set; } = default!;
    public string HomeSlug { get; set; } = default!;
    public string? HomeLogoUri { get; set; }
    public int? HomeRank { get; set; }
    public int HomeWins { get; set; }
    public int HomeLosses { get; set; }
    public int HomeConferenceWins { get; set; }
    public int HomeConferenceLosses { get; set; }

    // Odds & Betting
    public string? Spread { get; set; }
    public double? AwaySpread { get; set; }
    public double? HomeSpread { get; set; }
    public double? OverUnder { get; set; }

    // Optional enhancements
    public bool InsightAvailable { get; set; } = false;
}