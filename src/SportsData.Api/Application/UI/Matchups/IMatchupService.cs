using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Matchups;

public interface IMatchupService
{
    Task<Result<MatchupPreviewDto>> GetPreviewById(Guid contestId, CancellationToken cancellationToken = default);
}
