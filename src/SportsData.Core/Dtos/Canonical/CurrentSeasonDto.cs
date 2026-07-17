using System;
using System.Collections.Generic;

namespace SportsData.Core.Dtos.Canonical;

/// <summary>
/// The current-or-upcoming season for a sport, with its phases. "Current" is
/// resolved from the data (earliest season whose EndDate is still in the
/// future), so during a season it returns the in-progress one and during the
/// off-season it returns the next upcoming one — no calendar heuristic.
/// </summary>
public record CurrentSeasonDto
{
    public int SeasonYear { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public List<SeasonPhaseDto> Phases { get; init; } = [];
}

/// <summary>
/// One phase of a season. <see cref="TypeCode"/>: 1 = Preseason,
/// 2 = Regular Season, 3 = Postseason, 4 = Off Season.
/// </summary>
public record SeasonPhaseDto
{
    public int TypeCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
}
