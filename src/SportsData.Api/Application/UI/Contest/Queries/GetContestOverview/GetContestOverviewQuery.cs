using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Contest.Queries.GetContestOverview;

public class GetContestOverviewQuery
{
    public required Guid ContestId { get; init; }

    public required Sport Sport { get; init; }
}
