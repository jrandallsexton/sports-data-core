using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Contest.Queries.GetContestPlayLog;

public class GetContestPlayLogQuery
{
    public required Guid ContestId { get; init; }

    public required Sport Sport { get; init; }
}
