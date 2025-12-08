using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage.Dtos;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues;

public interface ILeagueService
{
    Task<Result<Guid>> CreateAsync(CreateLeagueRequest request, Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<Result<Guid?>> JoinLeague(Guid leagueId, Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<LeagueWeekMatchupsDto>> GetMatchupsForLeagueWeekAsync(Guid userId, Guid leagueId, int week,
        CancellationToken cancellationToken = default);

    Task<Result<Guid>> DeleteLeague(
        Guid userId,
        Guid leagueId,
        CancellationToken cancellationToken = default);

    Task<Result<List<PublicLeagueDto>>> GetPublicLeagues(Guid userId);

    Task<Result<LeagueWeekOverviewDto>> GetLeagueWeekOverview(
        Guid leagueId,
        int week);

    Task<Result<Guid>> GenerateLeagueWeekPreviews(Guid leagueId, int weekId);

    Task<Result<LeagueScoresByWeekDto>> GetLeagueScoresByWeek(Guid leagueId);
}