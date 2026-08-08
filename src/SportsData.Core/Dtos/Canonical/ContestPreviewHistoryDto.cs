using System;
using System.Collections.Generic;

namespace SportsData.Core.Dtos.Canonical
{
    /// <summary>
    /// Historical context for a matchup preview: recent head-to-head
    /// meetings plus each team's late-prior-season form. Assembled by
    /// Producer with preview-safe semantics baked in: preseason games
    /// excluded (system-testing data only), finalized/non-cancelled games
    /// only, and as-of filtering (no meeting on/after the target contest's
    /// start — the target can never leak into its own history).
    /// </summary>
    public class ContestPreviewHistoryDto
    {
        public List<PreviewGameResultDto> HeadToHead { get; set; } = [];

        public List<PreviewGameResultDto> AwayPriorSeasonGames { get; set; } = [];

        public List<PreviewGameResultDto> HomePriorSeasonGames { get; set; } = [];
    }

    /// <summary>
    /// One historical game, expressed entirely in team NAMES — deliberately
    /// no GUIDs. The preview prompt's output contract asks the model to
    /// echo a FranchiseSeasonId for its predicted winner; historical rows
    /// carry per-season ids that a model could echo by mistake. Historical
    /// blocks therefore contribute ZERO GUIDs to the payload — its only
    /// GUIDs are the ContestId and the two live Away/Home
    /// FranchiseSeasonIds.
    /// </summary>
    public class PreviewGameResultDto
    {
        public DateTime GameDate { get; set; }

        public int SeasonYear { get; set; }

        /// <summary>Season phase name (e.g. "Regular Season", "Postseason"); null on old rows.</summary>
        public string? Phase { get; set; }

        /// <summary>Event note when present (e.g. "NFC Championship").</summary>
        public string? Note { get; set; }

        public string HomeTeam { get; set; } = default!;

        public string AwayTeam { get; set; } = default!;

        public int? HomeScore { get; set; }

        public int? AwayScore { get; set; }

        /// <summary>Winning team name; null for a tie.</summary>
        public string? Winner { get; set; }

        /// <summary>Team that covered; null when no spread data exists (common pre-2012).</summary>
        public string? SpreadWinner { get; set; }

        // Market context for the historical game — same vocabulary as the
        // live matchup payload so the model reads one shape everywhere.
        // All null for pre-odds-era games (~pre-2022): result-only context.

        /// <summary>Line details text (e.g. "ARI -6.5") — self-documents the favorite.</summary>
        public string? Spread { get; set; }

        /// <summary>Closing/current spread, home-relative (negative = home favored).</summary>
        public double? HomeSpread { get; set; }

        /// <summary>Opening spread, home-relative — with HomeSpread exposes line movement.</summary>
        public double? HomeSpreadOpen { get; set; }

        public double? OverUnder { get; set; }

        public double? OverUnderOpen { get; set; }

        public double? OverOdds { get; set; }

        public double? UnderOdds { get; set; }

        /// <summary>"Over" / "Under"; null when no total was recorded.</summary>
        public string? OverUnderResult { get; set; }
    }
}
