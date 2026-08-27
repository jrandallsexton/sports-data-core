using System;
using System.Collections.Generic;

namespace SportsData.Core.Dtos.Canonical
{
    /// <summary>
    /// Flattened per-athlete per-contest box-score statline: canonical
    /// stat identity is <c>category.statName</c> (e.g.
    /// "passing.passingYards") → value. Powers Player Pick'em scoring —
    /// the scoring matrix keys on the same names, so the engine applies
    /// rules without knowing category structure.
    /// </summary>
    public class AthleteStatlineDto
    {
        public Guid AthleteSeasonId { get; set; }

        public Guid ContestId { get; set; }

        public Dictionary<string, decimal> Stats { get; set; } = [];
    }

    /// <summary>Batch request body for the statlines endpoint.</summary>
    public record GetAthleteStatlinesRequest(Guid[] ContestIds, Guid[] AthleteSeasonIds);
}
