using System;

namespace SportsData.Api.Infrastructure.Data.Canonical.Models;

public class CompetitionWithoutDrivesDto
{
    public Guid ContestId { get; set; }
    public string? ContestName { get; set; }
    public DateTime StartDateUtc { get; set; }
    public Guid CompetitionId { get; set; }
    public int PlayCount { get; set; }
    public int PlaysWithDriveId { get; set; }
    public string? LastPlayText { get; set; }
}

public class CompetitionWithoutMetricsDto
{
    public Guid ContestId { get; set; }
    public string? ContestName { get; set; }
    public DateTime StartDateUtc { get; set; }
    public Guid CompetitionId { get; set; }
    public int MetricCount { get; set; }
}
