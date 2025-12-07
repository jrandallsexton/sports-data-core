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

        public PlayLogDto? PlayLog { get; set; }

        public TeamStatsSectionDto? TeamStats { get; set; }

        public GameInfoDto? Info { get; set; }

        public CompetitionMetricDto? AwayMetrics { get; set; }

        public CompetitionMetricDto? HomeMetrics { get; set; }

        public List<MediaItemDto>? MediaItems { get; set; }

        //public MatchupAnalysisDto? MatchupAnalysis { get; set; } // Optional: Postgame insights
    }

    public class MediaItemDto
    {
        public string VideoId { get; set; } = null!; // e.g. "tOnedYAHqR8"
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string ChannelTitle { get; set; } = null!;
        public DateTime PublishedUtc { get; set; }

        public string ThumbnailUrl { get; set; } = null!;
        public string ThumbnailMediumUrl { get; set; } = null!;
        public string ThumbnailHighUrl { get; set; } = null!;

        public string YouTubeUrl => $"https://www.youtube.com/watch?v={VideoId}";
        public string EmbedUrl => $"https://www.youtube.com/embed/{VideoId}";
    }

    public class PlayLogDto
    {
        public required string AwayTeamSlug { get; set; }

        public required string HomeTeamSlug { get; set; }

        public required string AwayTeamLogoUrl { get; set; }

        public required string HomeTeamLogoUrl { get; set; }

        public List<PlayDto>? Plays { get; set; }
    }

    public class GameHeaderDto
    {
        public Guid ContestId { get; set; }

        public ContestStatus Status { get; set; }

        public string? WeekLabel { get; set; }

        public Guid SeasonWeekId { get; set; }

        public int SeasonYear { get; set; }

        public int SeasonWeekNumber { get; set; }

        public DateTime StartTimeUtc { get; set; }

        public string? VenueName { get; set; }

        public string? Location { get; set; }

        public TeamScoreDto? AwayTeam { get; set; }

        public TeamScoreDto? HomeTeam { get; set; }

        public List<QuarterScoreDto>? QuarterScores { get; set; }
    }

    public class TeamScoreDto
    {
        public Guid FranchiseSeasonId { get; set; }

        public string? GroupSeasonMap { get; set; }

        public string? Conference { get; set; }

        public required string Slug { get; set; }

        public string? DisplayName { get; set; }

        public string? LogoUrl { get; set; }

        public string? ColorPrimary { get; set; }

        public int? FinalScore { get; set; } // Optional if in-progress
    }

    public sealed class GameLeadersDto
    {
        /// <summary>One item per stat category (e.g., Passing Yards, Rushing TDs), each with home/away leaders.</summary>
        public List<LeaderCategoryDto> Categories { get; set; } = new();
    }

    public sealed class LeaderCategoryDto
    {
        /// <summary>Stable key you control (e.g., "passingYds", "rushingTd"). Avoid tying UI logic to display strings.</summary>
        public string CategoryId { get; set; } = null!;

        /// <summary>Human-readable display (e.g., "Passing Yards").</summary>
        public string CategoryName { get; set; } = null!;

        /// <summary>Optional short label for chips/columns (e.g., "PY").</summary>
        public string? Abbr { get; set; }

        /// <summary>Optional unit hint (e.g., "yds", "td").</summary>
        public string? Unit { get; set; }

        /// <summary>Controls UI ordering across categories.</summary>
        public int DisplayOrder { get; set; }

        /// <summary>Leaders for the home team in this category. Keep as an array to support ties.</summary>
        public TeamLeadersDto Home { get; set; } = new();

        /// <summary>Leaders for the away team in this category. Keep as an array to support ties.</summary>
        public TeamLeadersDto Away { get; set; } = new();
    }

    public sealed class TeamLeadersDto
    {
        /// <summary>Zero, one, or many leaders (ties). Empty list means “no leader” (e.g., no punts).</summary>
        public List<PlayerLeaderDto> Leaders { get; set; } = new();
    }

    public sealed class PlayerLeaderDto
    {
        /// <summary>Stable athlete id if available (preferred for routing/headshots).</summary>
        public string? PlayerId { get; set; }

        public string PlayerName { get; set; } = null!;

        public string? PlayerHeadshotUrl { get; set; }

        public string? Position { get; set; }

        public string? Jersey { get; set; }

        /// <summary>Stable team id/slug in case you need routing or badges.</summary>
        public string? TeamId { get; set; }

        /// <summary>Sortable numeric for the category’s primary metric (e.g., 287 for passing yards).</summary>
        public decimal? Value { get; set; }

        /// <summary>Pre-formatted display line (e.g., "23/34, 287 yds, 2 TD, 1 INT").</summary>
        public string? StatLine { get; set; }

        /// <summary>1-based rank within the team for this category. Equal ranks indicate a tie.</summary>
        public int Rank { get; set; } = 1;
    }


    //public class NarrativeSummaryDto
    //{
    //    public string? PreviewText { get; set; } // From MatchupPreview
    //    public string? ResultText { get; set; }  // From actual outcome
    //}

    public class WinProbabilityDto
    {
        public required string AwayTeamSlug { get; set; }

        public required string HomeTeamSlug { get; set; }

        public required string AwayTeamColor { get; set; }

        public required string HomeTeamColor { get; set; }

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

    public class CompetitionMetricDto
    {
        public Guid CompetitionId { get; set; }
        public Guid FranchiseSeasonId { get; set; }
        public int Season { get; set; }

        // Offense
        public decimal Ypp { get; set; }
        public decimal SuccessRate { get; set; }
        public decimal ExplosiveRate { get; set; }
        public decimal PointsPerDrive { get; set; }
        public decimal ThirdFourthRate { get; set; }
        public decimal? RzTdRate { get; set; }
        public decimal? RzScoreRate { get; set; }
        public decimal TimePossRatio { get; set; }

        // Defense (opponent)
        public decimal OppYpp { get; set; }
        public decimal OppSuccessRate { get; set; }
        public decimal OppExplosiveRate { get; set; }
        public decimal OppPointsPerDrive { get; set; }
        public decimal OppThirdFourthRate { get; set; }
        public decimal? OppRzTdRate { get; set; }
        public decimal? OppScoreTdRate { get; set; }

        // Special Teams / Discipline
        public decimal NetPunt { get; set; }
        public decimal FgPctShrunk { get; set; }
        public decimal FieldPosDiff { get; set; }
        public decimal TurnoverMarginPerDrive { get; set; }
        public decimal PenaltyYardsPerPlay { get; set; }
    }

}
