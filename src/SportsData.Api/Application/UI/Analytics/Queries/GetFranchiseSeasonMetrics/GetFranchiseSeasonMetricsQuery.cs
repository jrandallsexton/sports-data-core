using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Analytics.Queries.GetFranchiseSeasonMetrics;

public class GetFranchiseSeasonMetricsQuery
{
    public required int SeasonYear { get; init; }
    public required Sport Sport { get; init; }
}
