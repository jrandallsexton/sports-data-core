using System;
﻿namespace SportsData.Core.Dtos.Canonical
{
    public class Matchup
    {
        public Guid SeasonWeekId { get; set; }

        public int SeasonYear { get; set; }

        public int SeasonWeek { get; set; }

        public Guid ContestId { get; set; }

        public string? Headline { get; set; }

        public DateTime StartDateUtc { get; set; }

        /// <summary>
        /// Raw ESPN status type name (e.g. "STATUS_IN_PROGRESS", "STATUS_FINAL")
        /// for programmatic branching. Pair with <see cref="StatusDescription"/>
        /// for display.
        /// </summary>
        public string Status { get; set; } = null!;

        /// <summary>
        /// Human-readable status (e.g. "In Progress", "Final"). For display.
        /// </summary>
        public string StatusDescription { get; set; } = null!;

        // Venue Info
        public string? VenueName { get; set; }

        public string? VenueCity { get; set; }

        public string? VenueState { get; set; }

        public decimal? VenueLatitude { get; set; }

        public decimal? VenueLongitude { get; set; }

        // Away Team Info

        public required string AwaySlug { get; set; }

        public string? AwayColor { get; set; }

        public string AwayAbbreviation { get; set; } = null!;

        public int? AwayRank { get; set; }

        public int AwayWins { get; set; }

        public int AwayLosses { get; set; }

        public int AwayConferenceWins { get; set; }

        public int AwayConferenceLosses { get; set; }

        public string? AwayConferenceSlug { get; set; }

        public string? AwayGroupSeasonMap { get; set; }

        // Home Team Info

        public required string HomeSlug { get; set; }

        public string? HomeColor { get; set; }

        public string HomeAbbreviation { get; set; } = null!;

        public int? HomeRank { get; set; }

        public int HomeWins { get; set; }

        public int HomeLosses { get; set; }

        public int HomeConferenceWins { get; set; }

        public int HomeConferenceLosses { get; set; }

        public string? HomeConferenceSlug { get; set; }

        public string? HomeGroupSeasonMap { get; set; }

        public string? Spread { get; set; }

        public double? AwaySpread { get; set; }

        public double? HomeSpread { get; set; }

        public double? OverUnder { get; set; }

        public double? OverOdds { get; set; }

        public double? UnderOdds { get; set; }
    }
}
