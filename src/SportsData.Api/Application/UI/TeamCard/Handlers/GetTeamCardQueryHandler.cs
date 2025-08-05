using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Application.UI.TeamCard.Queries;
using SportsData.Api.Infrastructure.Data.Canonical;

namespace SportsData.Api.Application.UI.TeamCard.Handlers
{
    public interface IGetTeamCardQueryHandler
    {
        Task<TeamCardDto?> ExecuteAsync(
            GetTeamCardQuery query,
            CancellationToken cancellationToken = default);
    }

    public class GetTeamCardQueryHandler : IGetTeamCardQueryHandler
    {
        private readonly IProvideCanonicalData _canonicalDataProvider;

        public GetTeamCardQueryHandler(IProvideCanonicalData canonicalDataProvider)
        {
            _canonicalDataProvider = canonicalDataProvider;
        }

        public async Task<TeamCardDto?> ExecuteAsync(
            GetTeamCardQuery query,
            CancellationToken cancellationToken = default)
        {
            return await _canonicalDataProvider.ExecuteAsync(query, cancellationToken);
        }
    }
}
