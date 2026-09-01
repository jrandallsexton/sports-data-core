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

        /// <summary>Prior-season summary (record + metrics); null when the franchise has no prior season.</summary>
        public PreviewPriorSeasonSummaryDto? AwayPriorSeason { get; set; }

        public PreviewPriorSeasonSummaryDto? HomePriorSeason { get; set; }

        /// <summary>
        /// Spread-conditioned facts for the live line (e.g. "when did the
        /// favorite last beat ANYONE by this much?"). Null when the target
        /// contest has no line from the preferred/fallback providers.
        /// Everything here is spread-derived — consumers must gate display
        /// behind the gambling-content preference.
        /// </summary>
        public PreviewSpreadContextDto? SpreadContext { get; set; }
    }

    /// <summary>
    /// Facts conditioned on the target contest's CURRENT spread. Two data
    /// tiers with different floors: margin facts run on final scores (full
    /// corpus), ATS-bucket facts need historical spread VALUES (odds era,
    /// ~2022+). Each fact carries its own floor so consumers never imply a
    /// completeness the data doesn't have.
    /// </summary>
    public class PreviewSpreadContextDto
    {
        /// <summary>Favorite's Franchise.DisplayName (matches matchup Away/Home names).</summary>
        public string FavoriteTeam { get; set; } = default!;

        public string UnderdogTeam { get; set; } = default!;

        /// <summary>Absolute value of the spread (38.5 for "USC -38.5").</summary>
        public double Magnitude { get; set; }

        /// <summary>Line details text (e.g. "USC -38.5") — self-documents the favorite.</summary>
        public string? SpreadDetails { get; set; }

        /// <summary>Last time the favorite beat ANY opponent by ≥ Magnitude.</summary>
        public PreviewMarginFactDto FavoriteWonByMargin { get; set; } = new();

        /// <summary>Last time the underdog lost to ANY opponent by ≥ Magnitude.</summary>
        public PreviewMarginFactDto UnderdogLostByMargin { get; set; } = new();

        /// <summary>ATS record as a favorite of BucketThreshold+; null when the bucket doesn't apply (small lines).</summary>
        public PreviewAtsBucketFactDto? FavoriteAtsAsBigFavorite { get; set; }

        public PreviewAtsBucketFactDto? UnderdogAtsAsBigUnderdog { get; set; }
    }

    /// <summary>
    /// "When is the last time this team won/lost a game by ≥ X?" —
    /// score-margin tier, full corpus. A null LastGame means it has not
    /// happened within the searched window (SearchFloorSeason onward), which
    /// is itself the headline fact.
    /// </summary>
    public class PreviewMarginFactDto
    {
        public PreviewGameResultDto? LastGame { get; set; }

        /// <summary>The qualifying game's opponent record THAT season ("3-9"); null when unsourced.</summary>
        public string? OpponentSeasonRecord { get; set; }

        /// <summary>The opponent's record the season BEFORE the qualifying game ("2-10").</summary>
        public string? OpponentPriorSeasonRecord { get; set; }

        /// <summary>How many times it happened in the five seasons before the target contest.</summary>
        public int CountLastFiveSeasons { get; set; }

        /// <summary>Earliest season in the corpus for this franchise — the honest search floor.</summary>
        public int SearchFloorSeason { get; set; }

        /// <summary>
        /// The games behind <see cref="CountLastFiveSeasons"/>, newest first,
        /// capped at 10 — who was actually beaten (or lost to), with the
        /// opponent's record that season as quality context. The count remains
        /// the authority on totals; this list is its evidence.
        /// </summary>
        public List<PreviewMarginInstanceDto> WindowGames { get; set; } = [];
    }

    /// <summary>One qualifying game inside the five-season count window.</summary>
    public class PreviewMarginInstanceDto
    {
        public DateTime GameDate { get; set; }

        public int SeasonYear { get; set; }

        public string Opponent { get; set; } = default!;

        public int TeamScore { get; set; }

        public int OpponentScore { get; set; }

        /// <summary>Opponent's overall W-L that season ("7-5"); null when unsourced.</summary>
        public string? OpponentSeasonRecord { get; set; }
    }

    /// <summary>
    /// ATS record conditioned on spread size ("as a 35+ underdog") — market
    /// tier, spread VALUES exist ~2022+. Games counts decided results only
    /// (pushes / unsourced ATS results excluded).
    /// </summary>
    public class PreviewAtsBucketFactDto
    {
        /// <summary>Key-number bucket applied (largest of 3/7/10/14/21/28/35 ≤ Magnitude).</summary>
        public double Threshold { get; set; }

        public int Games { get; set; }

        public int Covers { get; set; }

        /// <summary>Floor season of the spread-value data tier (currently 2022).</summary>
        public int DataFloorSeason { get; set; }
    }

    /// <summary>
    /// A team's PRIOR season in summary: final record and the season-level
    /// advanced metrics. The early-season statistical signal — current-season
    /// stats/metrics are empty until several weeks in.
    /// </summary>
    public class PreviewPriorSeasonSummaryDto
    {
        public int SeasonYear { get; set; }

        public int Wins { get; set; }

        public int Losses { get; set; }

        /// <summary>
        /// Null when no conference record exists for that season (e.g.
        /// independents, or the vsconf record wasn't published). The
        /// payload serializer omits nulls, so absence reads as absence —
        /// never as a 0-0 record.
        /// </summary>
        public int? ConferenceWins { get; set; }

        public int? ConferenceLosses { get; set; }

        /// <summary>
        /// Prior-season FranchiseSeasonMetrics; null when not generated for
        /// that season. The API applies the same both-or-nothing symmetry
        /// rule as current-season metrics before the model sees them.
        /// </summary>
        public FranchiseSeasonMetricsDto? Metrics { get; set; }
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
