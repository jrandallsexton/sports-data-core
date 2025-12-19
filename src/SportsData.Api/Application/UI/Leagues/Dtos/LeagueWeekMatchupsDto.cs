using SportsData.Api.Application.UI.Contest;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.Dtos
{
    public class LeagueWeekMatchupsDto
    {
        public int SeasonYear { get; set; }

        public int WeekNumber { get; set; }

        public PickType PickType { get; set; }

        public bool UseConfidencePoints { get; set; }

        public List<MatchupForPickDto> Matchups { get; set; } = [];

        public class MatchupForPickDto
        {
            public Guid ContestId { get; set; }

            public string? HeadLine { get; set; }

            public List<ContestPredictionDto> Predictions { get; set; } = [];

            public DateTime StartDateUtc { get; set; }

            public ContestStatus Status { get; set; }

            public string? Broadcasts { get; set; }

            // Teams
            public string Away { get; set; } = default!;
            public string AwayShort { get; set; } = default!;
            public Guid AwayFranchiseSeasonId { get; set; }
            public string AwayLogoUri { get; set; } = null!;
            public string AwaySlug { get; set; } = default!;
            public string AwayColor { get; set; } = default!;
            public int? AwayRank { get; set; }
            public int AwayWins { get; set; }
            public int AwayLosses { get; set; }
            public int AwayConferenceWins { get; set; }
            public int AwayConferenceLosses { get; set; }

            public string Home { get; set; } = default!;
            public string HomeShort { get; set; } = default!;
            public Guid HomeFranchiseSeasonId { get; set; }
            public string HomeLogoUri { get; set; } = null!;
            public string HomeSlug { get; set; } = default!;
            public string HomeColor { get; set; } = default!;
            public int? HomeRank { get; set; }
            public int HomeWins { get; set; }
            public int HomeLosses { get; set; }
            public int HomeConferenceWins { get; set; }
            public int HomeConferenceLosses { get; set; }

            // Odds
            public string? SpreadCurrentDetails { get; set; }
            public decimal? SpreadCurrent { get; set; }
            public decimal? SpreadOpen { get; set; }
            public decimal? OverUnderCurrent { get; set; }
            public decimal? OverUnderOpen { get; set; }

            // Venue
            public string Venue { get; set; } = default!;
            public string VenueCity { get; set; } = default!;
            public string VenueState { get; set; } = default!;

            public Guid? AiWinnerFranchiseSeasonId { get; set; }
            public bool IsPreviewAvailable { get; set; }
            public bool IsPreviewReviewed { get; set; }

            // Result
            public bool IsComplete { get; set; }
            public int? AwayScore { get; set; }
            public int? HomeScore { get; set; }
            public Guid? WinnerFranchiseSeasonId { get; set; }
            public Guid? SpreadWinnerFranchiseSeasonId { get; set; }
            public OverUnderPick? OverUnderResult { get; set; }
            public DateTime? CompletedUtc { get; set; }
        }
    }
}