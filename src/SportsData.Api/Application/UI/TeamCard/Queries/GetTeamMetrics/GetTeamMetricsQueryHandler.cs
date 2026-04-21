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
                // Metrics simply haven't been generated yet for this team/season.
                // Treat as a normal empty-data case: log a warning for ops visibility
                // and return an empty DTO so the UI can render "no metrics yet"
                // without presenting the user an error.
                _logger.LogWarning(
                    "No metrics found for franchise season {FranchiseSeasonId}. Returning empty DTO.",
                    query.FranchiseSeasonId);

                return new Success<FranchiseSeasonMetricsDto>(new FranchiseSeasonMetricsDto());
            }

            return new Success<FranchiseSeasonMetricsDto>(dto);
        }
        catch (Exception ex)
        {
            // Metrics are a non-blocking enrichment surface — a backend failure here
            // (network blip, sport-specific client not wired up, Producer 404, etc.)
            // should never present the user with a hard error. Log at Error level so
            // ops still catches real regressions, but return an empty DTO so the UI
            // renders a friendly empty state instead of a 500.
            _logger.LogError(
                ex,
                "Error retrieving metrics for franchise season {FranchiseSeasonId}. Returning empty DTO.",
                query.FranchiseSeasonId);

            return new Success<FranchiseSeasonMetricsDto>(new FranchiseSeasonMetricsDto());
        }
    }
}
