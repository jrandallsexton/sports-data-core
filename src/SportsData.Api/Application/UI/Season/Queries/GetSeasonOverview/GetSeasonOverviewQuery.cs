using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Season.Queries.GetSeasonOverview;

public class GetSeasonOverviewQuery
{
    public required int SeasonYear { get; init; }

    public required Sport Sport { get; init; }
}
