using FluentValidation.Results;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.TeamCard.Queries.GetTeamMetrics;

public interface IGetTeamMetricsQueryHandler
{
    Task<Result<FranchiseSeasonMetricsDto>> ExecuteAsync(
        GetTeamMetricsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetTeamMetricsQueryHandler : IGetTeamMetricsQueryHandler
{
    private readonly ILogger<GetTeamMetricsQueryHandler> _logger;
    private readonly IFranchiseClientFactory _franchiseClientFactory;

    public GetTeamMetricsQueryHandler(
        ILogger<GetTeamMetricsQueryHandler> logger,
        IFranchiseClientFactory franchiseClientFactory)
    {
        _logger = logger;
        _franchiseClientFactory = franchiseClientFactory;
    }

    public async Task<Result<FranchiseSeasonMetricsDto>> ExecuteAsync(
        GetTeamMetricsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.FranchiseSeasonId == Guid.Empty)
        {
            return new Failure<FranchiseSeasonMetricsDto>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(query.FranchiseSeasonId), "Franchise season ID cannot be empty")]);
        }

        try
        {
            var client = _franchiseClientFactory.Resolve(query.Sport);
            var dto = await client.GetFranchiseSeasonMetricsByFranchiseSeasonId(query.FranchiseSeasonId, cancellationToken);

            if (dto == null)
            {
                _logger.LogWarning(
                    "No metrics found for franchise season {FranchiseSeasonId}",
                    query.FranchiseSeasonId);

                return new Failure<FranchiseSeasonMetricsDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(query.FranchiseSeasonId), "Metrics not found for this franchise season")]);
            }

            return new Success<FranchiseSeasonMetricsDto>(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving metrics for franchise season {FranchiseSeasonId}",
                query.FranchiseSeasonId);

            return new Failure<FranchiseSeasonMetricsDto>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure(nameof(query.FranchiseSeasonId), "Error retrieving metrics. Please try again later.")]);
        }
    }
}
