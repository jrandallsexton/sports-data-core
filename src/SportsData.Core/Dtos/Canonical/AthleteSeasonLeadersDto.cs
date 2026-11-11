using System;
using System.Collections.Generic;

namespace SportsData.Core.Dtos.Canonical
{
    /// <summary>
    /// A season leaderboard for one athlete stat (e.g. passing.passingYards),
    /// ranked from ESPN's season totals. The serialized shape is consumed by
    /// sd-ui/src/components/warRoom/LeaderCard.jsx.
    /// </summary>
    public class AthleteSeasonLeadersDto
    {
        public int SeasonYear { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Stat { get; set; } = string.Empty;

        /// <summary>ESPN's display name for the stat ("Passing Yards"); empty when no athlete has the stat.</summary>
        public string StatDisplayName { get; set; } = string.Empty;

        public bool PerGame { get; set; }

        /// <summary>
        /// Per-game boards only: an athlete qualifies by playing at least
        /// this share of the team's finalized games (0.75, the NCAA rule).
        /// Null for season totals.
        /// </summary>
        public decimal? MinGamesPlayedShare { get; set; }

        public List<AthleteSeasonLeaderDto> Leaders { get; set; } = [];
    }

    public class AthleteSeasonLeaderDto
    {
        /// <summary>Competition rank: tied values share a rank and the next rank skips (1, 2, 2, 4).</summary>
        public int Rank { get; set; }

        public Guid AthleteId { get; set; }
        public required string AthleteName { get; set; }

        /// <summary>Position abbreviation ("QB"); null when the roster row has none.</summary>
        public string? Position { get; set; }

        public required string FranchiseSlug { get; set; }

        /// <summary>Franchise.DisplayNameShort ("Texas").</summary>
        public required string FranchiseShortName { get; set; }

        public string? FranchiseAbbreviation { get; set; }
        public string? FranchiseColorHex { get; set; }

        /// <summary>Generated team mark; null when none exists (the UI falls back to a color roundel).</summary>
        public string? FranchiseLogoUrl { get; set; }

        /// <summary>Null when the athlete's stat document has no gamesPlayed.</summary>
        public int? GamesPlayed { get; set; }

        public decimal Value { get; set; }

        /// <summary>ESPN's formatted total ("3,169"), or the per-game value to one decimal.</summary>
        public required string DisplayValue { get; set; }
    }
}
