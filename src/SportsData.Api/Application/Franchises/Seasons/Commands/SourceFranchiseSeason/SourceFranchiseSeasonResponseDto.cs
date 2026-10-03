using SportsData.Api.Application.Franchises.Seasons.Commands.EnrichFranchiseSeason;

namespace SportsData.Api.Application.Franchises.Seasons.Commands.SourceFranchiseSeason;

/// <summary>
/// 202 body for the admin source action. Same HATEOAS shape as
/// <see cref="EnrichFranchiseSeasonResponseDto"/>: <c>ref</c> and
/// <c>links.self</c> point at the franchise season the sourcing was
/// requested for.
/// </summary>
public class SourceFranchiseSeasonResponseDto
{
    public Uri Ref { get; init; } = null!;

    public Dictionary<string, Uri> Links { get; init; } = new();

    public Guid FranchiseId { get; init; }

    public Guid FranchiseSeasonId { get; init; }

    public int SeasonYear { get; init; }

    /// <summary>The Producer's correlation id for the sourcing request; the Seq handle.</summary>
    public Guid CorrelationId { get; init; }
}
