using FluentValidation.Results;

using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Application.UI.TeamCard.Queries;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.TeamCard.Handlers;

public interface IGetTeamCardQueryHandler
{
    Task<Result<TeamCardDto>> ExecuteAsync(
        GetTeamCardQuery query,
        CancellationToken cancellationToken = default);
}

public class GetTeamCardQueryHandler : IGetTeamCardQueryHandler
{
    private readonly ILogger<GetTeamCardQueryHandler> _logger;
    private readonly IProvideCanonicalData _canonicalDataProvider;

    public GetTeamCardQueryHandler(
        ILogger<GetTeamCardQueryHandler> logger,
        IProvideCanonicalData canonicalDataProvider)
    {
        _logger = logger;
        _canonicalDataProvider = canonicalDataProvider;
    }

    public async Task<Result<TeamCardDto>> ExecuteAsync(
        GetTeamCardQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _canonicalDataProvider.GetTeamCard(query, cancellationToken);

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
