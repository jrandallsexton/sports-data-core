using System;
using System.Collections.Generic;

namespace SportsData.Core.Dtos.Canonical;

public record SeasonOverviewDto
{
    public int SeasonYear { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public List<SeasonWeekDto> Weeks { get; init; } = [];
    public List<SeasonPollSummaryDto> Polls { get; init; } = [];
}

public record SeasonWeekDto
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public string Label { get; init; } = string.Empty;
    public string SeasonPhaseName { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
}

public record SeasonPollSummaryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ShortName { get; init; } = string.Empty;
    public string? Slug { get; init; }
}
