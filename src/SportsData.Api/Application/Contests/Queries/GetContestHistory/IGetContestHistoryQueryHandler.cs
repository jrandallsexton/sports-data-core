using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Api.Application.Contests.Queries.GetContestHistory;

public interface IGetContestHistoryQueryHandler
{
    Task<Result<ContestPreviewHistoryDto>> ExecuteAsync(
        GetContestHistoryQuery query,
        CancellationToken cancellationToken);
}
