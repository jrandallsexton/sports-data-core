using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Picks.Dtos;

public record UserPickDto
{
    public Guid UserId { get; init; }

    public string? User { get; init; }

    public Guid Id { get; init; }

    public bool IsSynthetic { get; set; }

    public Guid ContestId { get; init; }

    public Guid FranchiseSeasonId { get; init; }

    public PickType PickType { get; init; }

    public int? ConfidencePoints { get; init; }

    public int? TiebreakerGuessTotal { get; init; }

    public bool? IsCorrect { get; init; }

    /// <summary>
    /// When the pick was scored. Set with IsCorrect null = a PUSH (decided,
    /// graded nobody); null = not yet scored. Lets consumers distinguish
    /// push from pending without a dedicated enum.
    /// </summary>
    public DateTime? ScoredAt { get; init; }

    public int? PointsAwarded { get; init; }
}