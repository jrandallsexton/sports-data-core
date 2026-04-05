using FluentValidation.Results;

using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.TeamCard.Queries.GetTeamStatistics;

public interface IGetTeamStatisticsQueryHandler
{
    Task<Result<FranchiseSeasonStatisticDto>> ExecuteAsync(
        GetTeamStatisticsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetTeamStatisticsQueryHandler : IGetTeamStatisticsQueryHandler
{
    private readonly ILogger<GetTeamStatisticsQueryHandler> _logger;
    private readonly IFranchiseClientFactory _franchiseClientFactory;
    private readonly IStatFormattingService _statFormatting;

    public GetTeamStatisticsQueryHandler(
        ILogger<GetTeamStatisticsQueryHandler> logger,
        IFranchiseClientFactory franchiseClientFactory,
        IStatFormattingService statFormatting)
    {
        _logger = logger;
        _franchiseClientFactory = franchiseClientFactory;
        _statFormatting = statFormatting;
    }

    public async Task<Result<FranchiseSeasonStatisticDto>> ExecuteAsync(
        GetTeamStatisticsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.FranchiseSeasonId == Guid.Empty)
        {
            return new Failure<FranchiseSeasonStatisticDto>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(query.FranchiseSeasonId), "Franchise season ID cannot be empty")]);
        }

        try
        {
            // TODO: multi-sport - resolve from context
            var client = _franchiseClientFactory.Resolve(Sport.FootballNcaa);
            var dto = await client.GetFranchiseSeasonStatistics(query.FranchiseSeasonId, cancellationToken);

            if (dto == null)
            {
                _logger.LogWarning(
                    "No statistics found for franchise season {FranchiseSeasonId}",
                    query.FranchiseSeasonId);

                return new Failure<FranchiseSeasonStatisticDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(query.FranchiseSeasonId), "Statistics not found for this franchise season")]);
            }

            _statFormatting.ApplyFriendlyLabelsAndFormatting(dto);
            return new Success<FranchiseSeasonStatisticDto>(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving statistics for franchise season {FranchiseSeasonId}",
                query.FranchiseSeasonId);

            return new Failure<FranchiseSeasonStatisticDto>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure(nameof(query.FranchiseSeasonId), "Error retrieving statistics. Please try again later.")]);
        }
    }
}
