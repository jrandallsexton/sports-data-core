using FluentValidation.Results;

using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Application.UI.TeamCard.Handlers;
using SportsData.Api.Application.UI.TeamCard.Queries;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.TeamCard
{
    public interface ITeamCardService
    {
        Task<Result<TeamCardDto?>> GetTeamCard(
            GetTeamCardQuery query,
            CancellationToken cancellationToken = default);
    }

    public class TeamCardService : ITeamCardService
    {
        private readonly ILogger<TeamCardService> _logger;
        private readonly IGetTeamCardQueryHandler _getTeamCardQueryHandler;

        public TeamCardService(
            ILogger<TeamCardService> logger,
            IGetTeamCardQueryHandler getTeamCardQueryHandler)
        {
            _logger = logger;
            _getTeamCardQueryHandler = getTeamCardQueryHandler;
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
    }
}
