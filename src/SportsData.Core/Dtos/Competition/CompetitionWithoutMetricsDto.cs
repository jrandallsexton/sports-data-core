using System;

namespace SportsData.Core.Dtos.Competition
{
    public class CompetitionWithoutMetricsDto
    {
        public Guid ContestId { get; set; }
        public string? ContestName { get; set; }
        public DateTime StartDateUtc { get; set; }
        public Guid CompetitionId { get; set; }
        public int MetricCount { get; set; }
    }
}
