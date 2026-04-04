using FluentValidation.Results;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.TeamCard.Queries.GetTeamCard;

public interface IGetTeamCardQueryHandler
{
    Task<Result<TeamCardDto>> ExecuteAsync(
        GetTeamCardQuery query,
        CancellationToken cancellationToken = default);
}

public class GetTeamCardQueryHandler : IGetTeamCardQueryHandler
{
    private readonly ILogger<GetTeamCardQueryHandler> _logger;
    private readonly IFranchiseClientFactory _franchiseClientFactory;

    public GetTeamCardQueryHandler(
        ILogger<GetTeamCardQueryHandler> logger,
        IFranchiseClientFactory franchiseClientFactory)
    {
        _logger = logger;
        _franchiseClientFactory = franchiseClientFactory;
    }

    public async Task<Result<TeamCardDto>> ExecuteAsync(
        GetTeamCardQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var mode = ModeMapper.ResolveMode(query.Sport, query.League);
            var client = _franchiseClientFactory.Resolve(mode);
            var result = await client.GetTeamCard(query.Slug, query.SeasonYear, cancellationToken);

            if (result is null)
            {
                _logger.LogWarning(
                    "Team card not found for sport={Sport}, league={League}, slug={Slug}, seasonYear={SeasonYear}",
                    query.Sport, query.League, query.Slug, query.SeasonYear);

                return new Failure<TeamCardDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure("TeamCard", "Team card not found")]);
            }

            return new Success<TeamCardDto>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving team card for sport={Sport}, league={League}, slug={Slug}, seasonYear={SeasonYear}",
                query.Sport, query.League, query.Slug, query.SeasonYear);

            return new Failure<TeamCardDto>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("TeamCard", "Error retrieving team card. Please try again later.")]);
        }
    }
}
