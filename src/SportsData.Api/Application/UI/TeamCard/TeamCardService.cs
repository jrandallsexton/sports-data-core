using FluentValidation.Results;

using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Application.UI.TeamCard.Handlers;
using SportsData.Api.Application.UI.TeamCard.Queries;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Api.Application.UI.TeamCard;

public interface ITeamCardService
{
    Task<Result<TeamCardDto?>> GetTeamCard(GetTeamCardQuery query, CancellationToken cancellationToken = default);
    Task<Result<FranchiseSeasonStatisticDto>> GetTeamStatistics(Guid franchiseSeasonId, CancellationToken cancellationToken = default);
    Task<Result<FranchiseSeasonMetricsDto>> GetTeamMetrics(Guid franchiseSeasonId, CancellationToken cancellationToken = default);
}

public class TeamCardService : ITeamCardService
{
    private readonly ILogger<TeamCardService> _logger;
    private readonly IGetTeamCardQueryHandler _getTeamCardQueryHandler;
    private readonly IProvideCanonicalData _canonicalDataProvider;
    private readonly IStatFormattingService _statFormatting;

    public TeamCardService(
        ILogger<TeamCardService> logger,
        IGetTeamCardQueryHandler getTeamCardQueryHandler,
        IProvideCanonicalData canonicalDataProvider,
        IStatFormattingService statFormatting)
    {
        _logger = logger;
        _getTeamCardQueryHandler = getTeamCardQueryHandler;
        _canonicalDataProvider = canonicalDataProvider;
        _statFormatting = statFormatting;
    }

    public async Task<Result<TeamCardDto?>> GetTeamCard(
        GetTeamCardQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await _getTeamCardQueryHandler.ExecuteAsync(query, cancellationToken);

        if (result is not null) return new Success<TeamCardDto?>(result);

        _logger.LogError("Failed to get team card for query: {@Query}", query);
        return new Failure<TeamCardDto?>(
            null,
            ResultStatus.NotFound,
            [new ValidationFailure("TeamCard", "Team card not found")]);
    }

    public async Task<Result<FranchiseSeasonStatisticDto>> GetTeamStatistics(
        Guid franchiseSeasonId,
        CancellationToken cancellationToken = default)
    {
        if (franchiseSeasonId == Guid.Empty)
        {
            return new Failure<FranchiseSeasonStatisticDto>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(franchiseSeasonId), "Franchise season ID cannot be empty")]);
        }

        try
        {
            var dto = await _canonicalDataProvider.GetFranchiseSeasonStatistics(franchiseSeasonId);

            if (dto == null)
            {
                _logger.LogWarning("No statistics found for franchise season {FranchiseSeasonId}", franchiseSeasonId);
                return new Failure<FranchiseSeasonStatisticDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(franchiseSeasonId), "Statistics not found for this franchise season")]);
            }

            _statFormatting.ApplyFriendlyLabelsAndFormatting(dto);
            return new Success<FranchiseSeasonStatisticDto>(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statistics for franchise season {FranchiseSeasonId}", franchiseSeasonId);
            return new Failure<FranchiseSeasonStatisticDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure(nameof(franchiseSeasonId), $"Error retrieving statistics: {ex.Message}")]);
        }
    }

    public async Task<Result<FranchiseSeasonMetricsDto>> GetTeamMetrics(
        Guid franchiseSeasonId,
        CancellationToken cancellationToken = default)
    {
        if (franchiseSeasonId == Guid.Empty)
        {
            return new Failure<FranchiseSeasonMetricsDto>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(franchiseSeasonId), "Franchise season ID cannot be empty")]);
        }

        try
        {
            var dto = await _canonicalDataProvider.GetFranchiseSeasonMetrics(franchiseSeasonId);

            if (dto == null)
            {
                _logger.LogWarning("No metrics found for franchise season {FranchiseSeasonId}", franchiseSeasonId);
                return new Failure<FranchiseSeasonMetricsDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(franchiseSeasonId), "Metrics not found for this franchise season")]);
            }

            return new Success<FranchiseSeasonMetricsDto>(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metrics for franchise season {FranchiseSeasonId}", franchiseSeasonId);
            return new Failure<FranchiseSeasonMetricsDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure(nameof(franchiseSeasonId), $"Error retrieving metrics: {ex.Message}")]);
        }
    }
}
