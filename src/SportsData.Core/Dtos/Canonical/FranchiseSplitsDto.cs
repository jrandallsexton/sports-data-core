using System;
using System.Collections.Generic;

namespace SportsData.Core.Dtos.Canonical
{
    /// <summary>
    /// A team's record over a filtered set of its games (War Room Splits),
    /// from the Producer's FranchiseContestFact projection. The serialized
    /// shape is consumed by sd-ui/src/components/warRoom/SplitsPanel.jsx.
    /// </summary>
    public class FranchiseSplitsDto
    {
        public string FranchiseSlug { get; set; } = string.Empty;
        public string FranchiseName { get; set; } = string.Empty;

        /// <summary>The season range the caller asked for.</summary>
        public int RequestedSeasonFrom { get; set; }

        /// <summary>
        /// The first season actually searched: the requested start, raised to
        /// the data floor of any filter in use (e.g. a line filter cannot see
        /// games before spreads were captured).
        /// </summary>
        public int SeasonFrom { get; set; }

        public int SeasonTo { get; set; }

        public FranchiseSplitsSummaryDto Summary { get; set; } = new();

        /// <summary>Matching games, newest first.</summary>
        public List<FranchiseSplitsGameDto> Games { get; set; } = [];

        /// <summary>
        /// Values for the opponent and opponent-conference pickers, over the
        /// requested season range and ignoring the other filters.
        /// </summary>
        public FranchiseSplitsOptionsDto Options { get; set; } = new();
    }

    public class FranchiseSplitsOptionsDto
    {
        /// <summary>Every opponent faced in the range, sorted by name.</summary>
        public List<FranchiseSplitsOpponentOptionDto> Opponents { get; set; } = [];

        /// <summary>Every opponent conference in the range (as of each game's season), sorted by label.</summary>
        public List<FranchiseSplitsConferenceOptionDto> Conferences { get; set; } = [];
    }

    public class FranchiseSplitsOpponentOptionDto
    {
        /// <summary>Franchise slug; the value for the opponent filter.</summary>
        public string Slug { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Games { get; set; }
    }

    public class FranchiseSplitsConferenceOptionDto
    {
        /// <summary>Conference abbreviation as projected (e.g. "midam"); the value for the opponent-conference filter.</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>Display name (e.g. "MAC").</summary>
        public string Label { get; set; } = string.Empty;
        public int Games { get; set; }
    }

    public class FranchiseSplitsSummaryDto
    {
        public int Games { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Ties { get; set; }

        /// <summary>ATS counts cover only games with a line.</summary>
        public int AtsWins { get; set; }
        public int AtsLosses { get; set; }
        public int AtsPushes { get; set; }

        /// <summary>Null when no matching game has a score.</summary>
        public decimal? AvgMargin { get; set; }
        public decimal? AvgPointsFor { get; set; }
        public decimal? AvgPointsAgainst { get; set; }

        /// <summary>Straight-up record as favorite (line below zero) and underdog (above zero); pick'em counts as neither.</summary>
        public int FavoriteWins { get; set; }
        public int FavoriteLosses { get; set; }
        public int UnderdogWins { get; set; }
        public int UnderdogLosses { get; set; }
    }

    public class FranchiseSplitsGameDto
    {
        public Guid ContestId { get; set; }
        public int SeasonYear { get; set; }
        public int? Week { get; set; }
        public int? SeasonPhaseTypeCode { get; set; }
        public DateTime KickoffUtc { get; set; }

        /// <summary>Home, Away or Neutral.</summary>
        public string Site { get; set; } = string.Empty;

        /// <summary>Noon, Afternoon or Night (local kickoff); null when only the date is known.</summary>
        public string? KickoffWindow { get; set; }

        public bool IsConferenceGame { get; set; }

        /// <summary>1-25, or 99 for unranked; null when unknown.</summary>
        public int? RankGoingIn { get; set; }

        public string OpponentSlug { get; set; } = string.Empty;
        public string OpponentName { get; set; } = string.Empty;

        /// <summary>1-25, or 99 for unranked; null when unknown.</summary>
        public int? OpponentRankGoingIn { get; set; }

        public int? PointsFor { get; set; }
        public int? PointsAgainst { get; set; }

        /// <summary>Win, Loss or Tie.</summary>
        public string? Result { get; set; }

        /// <summary>This team's line (negative = favored).</summary>
        public decimal? Spread { get; set; }

        /// <summary>Covered, NotCovered or Push; null without a line.</summary>
        public string? AtsResult { get; set; }
    }
}
