using FluentValidation.Results;

using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutMetrics;

public interface IGetCompetitionsWithoutMetricsQueryHandler
{
    Task<Result<List<CompetitionWithoutMetricsDto>>> ExecuteAsync(
        GetCompetitionsWithoutMetricsQuery query,
        CancellationToken cancellationToken);
}

public class GetCompetitionsWithoutMetricsQueryHandler : IGetCompetitionsWithoutMetricsQueryHandler
{
    private readonly IProvideCanonicalAdminData _canonicalAdminData;
    private readonly ILogger<GetCompetitionsWithoutMetricsQueryHandler> _logger;

    public GetCompetitionsWithoutMetricsQueryHandler(
        IProvideCanonicalAdminData canonicalAdminData,
        ILogger<GetCompetitionsWithoutMetricsQueryHandler> logger)
    {
        _canonicalAdminData = canonicalAdminData;
        _logger = logger;
    }

    public async Task<Result<List<CompetitionWithoutMetricsDto>>> ExecuteAsync(
        GetCompetitionsWithoutMetricsQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _canonicalAdminData.GetCompetitionsWithoutMetrics(cancellationToken);
            return new Success<List<CompetitionWithoutMetricsDto>>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get competitions without metrics");
            return new Failure<List<CompetitionWithoutMetricsDto>>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("competitions", $"Error retrieving competitions without metrics: {ex.Message}")]);
        }
    }
}
