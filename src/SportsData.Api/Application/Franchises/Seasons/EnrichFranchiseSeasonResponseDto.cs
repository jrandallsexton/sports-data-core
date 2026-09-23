namespace SportsData.Api.Application.Franchises.Seasons;

/// <summary>
/// 202 body for the admin enrich action. Same HATEOAS shape as
/// <see cref="FranchiseSeasonResponseDto"/>: <c>ref</c> and <c>links.self</c>
/// point at the franchise season the work was requested for.
/// </summary>
public class EnrichFranchiseSeasonResponseDto
{
    public Uri Ref { get; init; } = null!;

    public Dictionary<string, Uri> Links { get; init; } = new();

    public Guid FranchiseId { get; init; }

    public Guid FranchiseSeasonId { get; init; }

    public int SeasonYear { get; init; }

    /// <summary>Shared by all enrichment legs on the Producer; the Seq handle for this request.</summary>
    public Guid CorrelationId { get; init; }
}
