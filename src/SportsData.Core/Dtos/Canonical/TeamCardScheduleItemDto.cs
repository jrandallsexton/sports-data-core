using System;

namespace SportsData.Core.Dtos.Canonical;

public record TeamCardScheduleItemDto
{
    public Guid ContestId { get; init; }

    public int Week { get; init; }

    public DateTime Date { get; init; }

    public string Opponent { get; init; } = default!;

    public string OpponentShortName { get; init; } = default!;

    public string OpponentSlug { get; init; } = default!;

    public string Location { get; init; } = default!;

    public string LocationType { get; init; } = default!;

    public string? Status { get; init; }

    public DateTime? FinalizedUtc { get; init; }

    public int? AwayScore { get; init; }

    public int? HomeScore { get; init; }

    public bool WasWinner { get; init; }
}
