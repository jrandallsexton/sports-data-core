namespace SportsData.Api.Application.UI.Picks.Dtos;

public class UserPickDto
{
    public Guid Id { get; set; }

    public Guid ContestId { get; set; }

    public Guid FranchiseId { get; set; }

    public UserPickType PickType { get; set; }

    public int? ConfidencePoints { get; set; }

    public int? TiebreakerGuessTotal { get; set; }

    public bool? IsCorrect { get; set; }

    public int? PointsAwarded { get; set; }
}