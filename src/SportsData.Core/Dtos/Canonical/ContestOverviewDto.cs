using SportsData.Core.Common;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Dtos.Canonical
{
    public class ContestOverviewDto
    {
        public GameHeaderDto? Header { get; set; }

        public GameLeadersDto? Leaders { get; set; }

        //public NarrativeSummaryDto? Summary { get; set; }

        public WinProbabilityDto? WinProbability { get; set; }

        public List<PlayDto>? PlayLog { get; set; }

        public TeamStatsSectionDto? TeamStats { get; set; }

        public GameInfoDto? Info { get; set; }

        //public MatchupAnalysisDto? MatchupAnalysis { get; set; } // Optional: Postgame insights
    }

    public class GameHeaderDto
    {
        public Guid ContestId { get; set; }

        public ContestStatus Status { get; set; }

        public string? WeekLabel { get; set; }

        public DateTime StartTimeUtc { get; set; }

        public string? VenueName { get; set; }

        public string? Location { get; set; }

        public TeamScoreDto? HomeTeam { get; set; }

        public TeamScoreDto? AwayTeam { get; set; }

        public List<QuarterScoreDto>? QuarterScores { get; set; }
    }

    public class TeamScoreDto
    {
        public Guid FranchiseSeasonId { get; set; }

        public string? DisplayName { get; set; }

        public string? LogoUrl { get; set; }

        public string? ColorPrimary { get; set; }

        public int? FinalScore { get; set; } // Optional if in-progress
    }

    public class GameLeadersDto
    {
        public List<StatLeaderDto>? HomeLeaders { get; set; }
        public List<StatLeaderDto>? AwayLeaders { get; set; }
    }

    public class StatLeaderDto
    {
        public string? Category { get; set; } // Passing, Rushing, etc.

        public string? PlayerName { get; set; }

        public string? StatLine { get; set; } // e.g. "21/33, 295 YDS, 3 TD"
    }

    //public class NarrativeSummaryDto
    //{
    //    public string? PreviewText { get; set; } // From MatchupPreview
    //    public string? ResultText { get; set; }  // From actual outcome
    //}

    public class WinProbabilityDto
    {
        public List<WinProbabilityPointDto>? Points { get; set; }

        public int? FinalHomeWinPercent { get; set; }

        public int? FinalAwayWinPercent { get; set; }
    }

    public class PlayDto
    {
        public int Ordinal { get; set; }

        public int Quarter { get; set; }

        public string? Team { get; set; }

        public Guid FranchiseSeasonId { get; set; }

        public string? Description { get; set; }

        public string? TimeRemaining { get; set; }

        public bool IsScoringPlay { get; set; }

        public bool IsKeyPlay { get; set; }
    }

    public class TeamStatsSectionDto
    {
        public TeamStatBlockDto? HomeTeam { get; set; }

        public TeamStatBlockDto? AwayTeam { get; set; }
    }

    public class TeamStatBlockDto
    {
        public Dictionary<string, string>? Stats { get; set; } // "Total Yards": "412", etc.
    }

    public class GameInfoDto
    {
        public DateTime StartDateUtc { get; set; }

        public string? Broadcast { get; set; } // e.g., "ESPN"

        public string? Venue { get; set; }

        public string? VenueCity { get; set; }

        public string? VenueState { get; set; }

        public string? VenueImageUrl { get; set; }

        public int? Attendance { get; set; }
    }

    public class WinProbabilityPointDto
    {
        public string? GameClock { get; set; }

        public int HomeWinPercent { get; set; }

        public int AwayWinPercent { get; set; }

        public int Quarter { get; set; }
    }

    public class QuarterScoreDto
    {
        public int Quarter { get; set; }

        public double HomeScore { get; set; }

        public double AwayScore { get; set; }
    }

    //public class MatchupAnalysisDto
    //{
    //    public string? PredictedSummary { get; set; }
    //    public string? ActualResultSummary { get; set; }
    //    public string? ModelAccuracyNotes { get; set; }
    //    public string? WhereItWasRight { get; set; }
    //    public string? WhereItWasWrong { get; set; }
    //}
}
