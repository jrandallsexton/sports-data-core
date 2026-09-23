namespace SportsData.Api.Application.Franchises.Seasons.Commands.EnrichFranchiseSeason;

public class EnrichFranchiseSeasonResponseDto
{
    public Guid FranchiseId { get; init; }

    public Guid FranchiseSeasonId { get; init; }

    public int SeasonYear { get; init; }

    /// <summary>Shared by all enrichment legs on the Producer; the Seq handle for this request.</summary>
    public Guid CorrelationId { get; init; }
}
