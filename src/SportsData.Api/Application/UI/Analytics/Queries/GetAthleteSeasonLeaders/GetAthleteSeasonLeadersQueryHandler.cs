using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Athlete;

namespace SportsData.Api.Application.UI.Analytics.Queries.GetAthleteSeasonLeaders;

public interface IGetAthleteSeasonLeadersQueryHandler
{
    Task<Result<AthleteSeasonLeadersDto>> ExecuteAsync(
        GetAthleteSeasonLeadersQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// War Room leader cards. Relays to the sport's Producer, which owns the
/// ranking rules (newest stat doc, per-game qualification, FBS scope) and
/// validates the inputs.
/// </summary>
public class GetAthleteSeasonLeadersQueryHandler : IGetAthleteSeasonLeadersQueryHandler
{
    private readonly IAthleteClientFactory _athleteClientFactory;

    public GetAthleteSeasonLeadersQueryHandler(IAthleteClientFactory athleteClientFactory)
    {
        _athleteClientFactory = athleteClientFactory;
    }

    public async Task<Result<AthleteSeasonLeadersDto>> ExecuteAsync(
        GetAthleteSeasonLeadersQuery query,
        CancellationToken cancellationToken = default)
    {
        var client = _athleteClientFactory.Resolve(query.Sport);

        return await client.GetAthleteSeasonLeaders(
            query.SeasonYear,
            query.Category,
            query.Stat,
            query.PerGame,
            query.Limit,
            cancellationToken);
    }
}
