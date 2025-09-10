namespace SportsData.Api.Application.UI.Leagues.Dtos
{
    public class LeagueWeekMatchupsDto
    {
        public int SeasonYear { get; set; }

        public int WeekNumber { get; set; }

        public PickType PickType { get; set; }

        public List<MatchupForPickDto> Matchups { get; set; } = new();

        public class MatchupForPickDto
        {
            public Guid ContestId { get; set; }
            public DateTime StartDateUtc { get; set; }

            // Teams
            public string Away { get; set; } = default!;
            public string AwayShort { get; set; } = default!;
            public Guid AwayFranchiseSeasonId { get; set; }
            public string AwayLogoUri { get; set; } = null!;
            public string AwaySlug { get; set; } = default!;
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
            public int? HomeRank { get; set; }
            public int HomeWins { get; set; }
            public int HomeLosses { get; set; }
            public int HomeConferenceWins { get; set; }
            public int HomeConferenceLosses { get; set; }

            // Odds
            public decimal? AwaySpread { get; set; }
            public decimal? HomeSpread { get; set; }
            public decimal? OverUnder { get; set; }

            // Venue
            public string Venue { get; set; } = default!;
            public string VenueCity { get; set; } = default!;
            public string VenueState { get; set; } = default!;

            public bool IsPreviewAvailable { get; set; }

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
