using SportsData.Core.Common;

using System;

namespace SportsData.Core.Dtos.Competition
{
    /// <summary>Produced per sport by Producer (GET api/competitions/without-drives); API aggregates the sports.</summary>
    public class CompetitionWithoutDrivesDto
    {
        /// <summary>The contest row's own sport (stamped by the Producer that owns it).</summary>
        public Sport Sport { get; set; }

        public Guid ContestId { get; set; }
        public string? ContestName { get; set; }
        public DateTime StartDateUtc { get; set; }
        public Guid CompetitionId { get; set; }
        public int PlayCount { get; set; }
        public int PlaysWithDriveId { get; set; }
        public string? LastPlayText { get; set; }
    }
}
