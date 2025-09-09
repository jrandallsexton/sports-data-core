using FluentValidation.Results;

using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Application.UI.TeamCard.Handlers;
using SportsData.Api.Application.UI.TeamCard.Queries;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.TeamCard
{
    public interface ITeamCardService
    {
        Task<Result<TeamCardDto?>> GetTeamCard(
            GetTeamCardQuery query,
            CancellationToken cancellationToken = default);

        Task<FranchiseSeasonStatisticDto> GetTeamStatistics(Guid franchiseSeasonId);
    }

    public class TeamCardService : ITeamCardService
    {
        private readonly ILogger<TeamCardService> _logger;
        private readonly IGetTeamCardQueryHandler _getTeamCardQueryHandler;
        private readonly IProvideCanonicalData _canonicalDataProvider;

        public TeamCardService(
            ILogger<TeamCardService> logger,
            IGetTeamCardQueryHandler getTeamCardQueryHandler,
            IProvideCanonicalData canonicalDataProvider)
        {
            _logger = logger;
            _getTeamCardQueryHandler = getTeamCardQueryHandler;
            _canonicalDataProvider = canonicalDataProvider;
        }

        public async Task<Result<TeamCardDto?>> GetTeamCard(
            GetTeamCardQuery query,
            CancellationToken cancellationToken = default)
        {
            var result = await _getTeamCardQueryHandler.ExecuteAsync(query, cancellationToken);

            if (result is not null)
            {
                return new Success<TeamCardDto?>(result);
            }
            else
            {
                _logger.LogError("Failed to get team card for query: {@Query}", query);
                return new Failure<TeamCardDto?>(
                    null,
                    ResultStatus.NotFound,
                    [new ValidationFailure("TeamCard", "Team card not found")]);
            }
        }

        public async Task<FranchiseSeasonStatisticDto> GetTeamStatistics(Guid franchiseSeasonId)
        {
            return await _canonicalDataProvider
                .GetFranchiseSeasonStatistics(franchiseSeasonId);
        }
    }
}
