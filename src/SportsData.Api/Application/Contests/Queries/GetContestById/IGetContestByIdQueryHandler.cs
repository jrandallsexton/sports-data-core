using SportsData.Api.Application.Contests.Queries.GetContestById.Dtos;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Contests.Queries.GetContestById;

public interface IGetContestByIdQueryHandler
{
    Task<Result<ContestDetailResponseDto>> ExecuteAsync(GetContestByIdQuery query, CancellationToken cancellationToken);
}
