using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.TeamCard.Queries.GetTeamMetrics;

public class GetTeamMetricsQuery
{
    public required Guid FranchiseSeasonId { get; init; }
    public required Sport Sport { get; init; }
}
