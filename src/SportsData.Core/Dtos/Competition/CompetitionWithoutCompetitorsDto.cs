using System;

namespace SportsData.Core.Dtos.Competition
{
    public class CompetitionWithoutCompetitorsDto
    {
        public Guid ContestId { get; set; }
        public Guid CompetitionId { get; set; }
        public DateTime StartDateUtc { get; set; }
        public string? CompetitionName { get; set; }
        public int CompetitorCount { get; set; }
    }
}
