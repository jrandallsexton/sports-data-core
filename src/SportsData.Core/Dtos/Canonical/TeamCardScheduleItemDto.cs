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

    /// <summary>
    /// Raw ESPN status type name (e.g. "STATUS_IN_PROGRESS", "STATUS_FINAL")
    /// for programmatic branching. Pair with <see cref="StatusDescription"/>
    /// for display.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Human-readable status (e.g. "In Progress", "Final"). For display.
    /// </summary>
    public string? StatusDescription { get; init; }

    public DateTime? FinalizedUtc { get; init; }

    public int? AwayScore { get; init; }

    public int? HomeScore { get; init; }

    public bool? WasWinner { get; init; }
}
