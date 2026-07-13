using System;
﻿namespace SportsData.Core.Dtos.Canonical
{
    public class MatchupResult
    {
        public Guid ContestId { get; set; }

        public Guid SeasonWeekId { get; set; }

        public Guid AwayFranchiseSeasonId { get; set; }

        public Guid HomeFranchiseSeasonId { get; set; }

        public int AwayScore { get; set; }

        public int HomeScore { get; set; }

        public double? Spread { get; set; }

        // Nullable: unset until Producer's ContestEnrichmentProcessor populates
        // the winner. Pre-enrichment reads would silently see Guid.Empty if this
        // were non-nullable, causing every pick to be scored as incorrect.
        public Guid? WinnerFranchiseSeasonId { get; set; }

        public Guid? SpreadWinnerFranchiseSeasonId { get; set; } // nullable if there was no spread

        // Nullable: NULL until enrichment runs. PickScoringProcessor/Service
        // gate on this — the canonical "is this contest scoreable" signal.
        public DateTime? FinalizedUtc { get; set; }

        // Team abbreviations + franchise ids (from the FranchiseSeason join).
        // Used to compose the pick-result notification and to resolve which side
        // the user picked — UserPick stores FranchiseId, not FranchiseSeasonId.
        // Nullable — a franchise-season without an abbreviation degrades gracefully.
        public string? AwayAbbreviation { get; set; }

        public string? HomeAbbreviation { get; set; }

        public Guid? AwayFranchiseId { get; set; }

        public Guid? HomeFranchiseId { get; set; }
    }
}
