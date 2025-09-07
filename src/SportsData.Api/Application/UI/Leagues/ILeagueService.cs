using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage.Dtos;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues;

public interface ILeagueService
{
    Task<Guid> CreateAsync(CreateLeagueRequest request, Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<Result<Guid?>> JoinLeague(Guid leagueId, Guid userId,
        CancellationToken cancellationToken = default);

    Task<LeagueWeekMatchupsDto> GetMatchupsForLeagueWeekAsync(Guid userId, Guid leagueId, int week,
        CancellationToken cancellationToken = default);

    Task<Guid> DeleteLeague(
        Guid userId,
        Guid leagueId,
        CancellationToken cancellationToken = default);

    Task<List<PublicLeagueDto>> GetPublicLeagues(Guid userId);

    Task<LeagueWeekOverviewDto> GetLeagueWeekOverview(
        Guid leagueId,
        int week);
}