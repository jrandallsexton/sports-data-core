using SportsData.Core.Common;

using System;

namespace SportsData.Core.Dtos.Competition
{
    /// <summary>
    /// A competition whose competitor count is not 2. Produced per sport by
    /// Producer (GET api/competitions/without-competitors); API aggregates
    /// the sports for the admin diagnostics page.
    /// </summary>
    public class CompetitionWithoutCompetitorsDto
    {
        /// <summary>The contest row's own sport (stamped by the Producer that owns it).</summary>
        public Sport Sport { get; set; }

        public Guid ContestId { get; set; }
        public Guid CompetitionId { get; set; }
        public DateTime StartDateUtc { get; set; }
        public string? CompetitionName { get; set; }
        public int CompetitorCount { get; set; }
    }
}
