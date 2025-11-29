namespace SportsData.Api.Application.UI.Picks.Dtos;

public record UserPickDto
{
    public Guid UserId { get; init; }

    public string? User { get; init; }

    public Guid Id { get; init; }

    public bool IsSynthetic { get; set; }

    public Guid ContestId { get; init; }

    public Guid FranchiseId { get; init; }

    public UserPickType PickType { get; init; }

    public int? ConfidencePoints { get; init; }

    public int? TiebreakerGuessTotal { get; init; }

    public bool? IsCorrect { get; init; }

    public int? PointsAwarded { get; init; }
}