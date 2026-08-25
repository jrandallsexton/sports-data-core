using System;
using System.Collections.Generic;

namespace SportsData.Core.Dtos.Canonical
{
    /// <summary>
    /// One athlete's week-matchup summary: identity, team, the week's
    /// opponent with its defensive allowance, and structured current/
    /// previous season stat blocks. Canonical athlete data — the API's
    /// pick'em surfaces consume it, but nothing in it is game-specific.
    /// The serialized shape is frozen by sd-ui/src/api/playerPickemApi.js
    /// and sd-mobile/src/services/api/playerPickemApi.ts — field renames
    /// are breaking changes.
    /// </summary>
    public class AthleteMatchupSummaryDto
    {
        public Guid AthleteId { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string TeamName { get; set; }
        public required string TeamSlug { get; set; }
        /// <summary>"QB" | "RB" | "WR" | "TE" | "K" (position abbreviation).</summary>
        public required string Position { get; set; }

        /// <summary>Null on a bye week.</summary>
        public string? OpponentName { get; set; }
        public string? OpponentSlug { get; set; }

        /// <summary>
        /// The week opponent's relevant defensive allowance per game,
        /// aggregated from what THEIR opponents actually gained against
        /// them (ESPN's own yardsAllowed team stats are zero-filled):
        /// net pass yds allowed/G for QB/WR/TE, rush yds allowed/G for
        /// RB, points allowed/G for K. Prior-season values when the
        /// opponent has no current-season games yet.
        /// </summary>
        public decimal? OpponentDefPerGame { get; set; }

        /// <summary>Null before the athlete's first game of the season.</summary>
        public AthleteSeasonStatBlockDto? CurrentSeason { get; set; }

        /// <summary>Null for athletes with no prior-season record (true freshmen).</summary>
        public AthleteSeasonStatBlockDto? PreviousSeason { get; set; }
    }

    /// <summary>
    /// One season of position-relevant stats. Both seasons use the SAME
    /// shape so consumers can render prior season directly beneath current
    /// season in the same columns. Keys are the consumer contract's stat
    /// keys (cmpPct, passYds, rushAtt, receptions, fgMade, ...).
    /// </summary>
    public class AthleteSeasonStatBlockDto
    {
        public int SeasonYear { get; set; }
        public int GamesPlayed { get; set; }
        public Dictionary<string, decimal> Stats { get; set; } = new Dictionary<string, decimal>();
    }

    public class AthleteMatchupSummariesDto
    {
        public List<AthleteMatchupSummaryDto> Athletes { get; set; } = new List<AthleteMatchupSummaryDto>();
    }
}
