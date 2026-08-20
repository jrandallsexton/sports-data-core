using System;
using System.Collections.Generic;
﻿using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Core.Dtos.Canonical
{
    public class MatchupForPreviewDto
    {
        public Sport Sport { get; set; }
        public int SeasonYear { get; set; }
        public int WeekNumber { get; set; }

        public Guid ContestId { get; set; }

        public string? HeadLine { get; set; }
        public DateTime StartDateUtc { get; set; }

        /// <summary>
        /// Season phase name for the TARGET game ("Preseason",
        /// "Regular Season", "Postseason"). The payload's history rows
        /// already carry per-game Phase; this closes the asymmetry so the
        /// model can calibrate tone (preseason = experimental lineups;
        /// postseason = elimination stakes).
        /// </summary>
        public string? SeasonPhase { get; set; }
        /// <summary>
        /// Raw ESPN status type name (e.g. "STATUS_IN_PROGRESS", "STATUS_FINAL")
        /// for programmatic branching. Pair with <see cref="StatusDescription"/>
        /// for display.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Human-readable status (e.g. "In Progress", "Final"). For display.
        /// </summary>
        public string? StatusDescription { get; set; }

        public required string Venue { get; set; }
        public required string VenueCity { get; set; }
        public string? VenueState { get; set; }

        public Guid AwayFranchiseSeasonId { get; set; }
        public required string Away { get; set; }
        public required string AwaySlug { get; set; }
        public int? AwayRank { get; set; }
        public required string AwayConferenceSlug { get; set; }
        public int AwayWins { get; set; }
        public int AwayLosses { get; set; }
        public int AwayConferenceWins { get; set; }
        public int AwayConferenceLosses { get; set; }
        public FranchiseSeasonModelStatsDto? AwayStats { get; set; }
        public FranchiseSeasonMetricsDto? AwayMetrics { get; set; }
        public List<FranchiseSeasonCompetitionResultDto>? AwayCompetitionResults { get; set; }

        public Guid HomeFranchiseSeasonId { get; set; }
        public required string Home { get; set; }
        public required string HomeSlug { get; set; }
        public int? HomeRank { get; set; }
        public required string HomeConferenceSlug { get; set; }
        public int HomeWins { get; set; }
        public int HomeLosses { get; set; }
        public int HomeConferenceWins { get; set; }
        public int HomeConferenceLosses { get; set; }
        public FranchiseSeasonModelStatsDto? HomeStats { get; set; }
        public FranchiseSeasonMetricsDto? HomeMetrics { get; set; }
        public List<FranchiseSeasonCompetitionResultDto>? HomeCompetitionResults { get; set; }

        /// <summary>
        /// Historical blocks (docs/metrics-modeling/matchup-preview-data-inputs.md
        /// §3b/3c): last 5 head-to-head meetings and each team's last 5
        /// prior-season games. Names-only rows (no GUIDs — the model must
        /// only ever echo the two live FranchiseSeasonIds above). Populated
        /// by the API's preview assembly; null when history fetch fails
        /// (graceful degradation).
        /// </summary>
        public List<PreviewGameResultDto>? HeadToHead { get; set; }
        public List<PreviewGameResultDto>? AwayPriorSeasonGames { get; set; }
        public List<PreviewGameResultDto>? HomePriorSeasonGames { get; set; }
        public PreviewPriorSeasonSummaryDto? AwayPriorSeason { get; set; }
        public PreviewPriorSeasonSummaryDto? HomePriorSeason { get; set; }

        /// <summary>
        /// Spread-conditioned facts for the live line (last time each side
        /// won/lost by the spread's magnitude with opponent-quality context,
        /// ATS records at the key-number bucket). Names-and-numbers only —
        /// zero GUIDs, same rule as the other historical blocks. Computed
        /// preview-safe (as-of the target's start) in Producer; null when
        /// the contest has no line or the history fetch fails.
        /// </summary>
        public PreviewSpreadContextDto? SpreadContext { get; set; }

        public string? Spread { get; set; }             // co."Details"
        public double? AwaySpread { get; set; }
        public double? HomeSpread { get; set; }
        public double? OverUnder { get; set; }
        public double? OverOdds { get; set; }
        public double? UnderOdds { get; set; }
    }

}
