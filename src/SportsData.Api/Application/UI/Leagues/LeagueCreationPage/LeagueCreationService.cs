using SportsData.Api.Application.UI.Leagues.LeagueCreationPage.Commands;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage.Dtos;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.LeagueCreationPage
{
    public interface ILeagueCreationService
    {
        Task<Guid> CreateAsync(CreateLeagueRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
    }

    public class LeagueCreationService : ILeagueCreationService
    {
        private readonly IProvideCanonicalData _canonicalDataProvider;
        private readonly ICreateLeagueCommandHandler _handler;

        public LeagueCreationService(
            IProvideCanonicalData canonicalDataProvider,
            ICreateLeagueCommandHandler handler)
        {
            _canonicalDataProvider = canonicalDataProvider;
            _handler = handler;
        }

        public async Task<Guid> CreateAsync(
            CreateLeagueRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            // === Validation ===
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("League name is required.");

            if (request.ConferenceSlugs is null || !request.ConferenceSlugs.Any())
                throw new ArgumentException("At least one conference must be selected.");

            // === Enum Resolution ===
            if (!Enum.TryParse<PickType>(request.PickType, ignoreCase: true, out var pickType))
                throw new ArgumentException($"Invalid pick type: {request.PickType}");

            if (!Enum.TryParse<TiebreakerType>(request.TiebreakerType, ignoreCase: true, out var tiebreakerType))
                throw new ArgumentException($"Invalid tiebreaker type: {request.TiebreakerType}");

            if (!Enum.TryParse<TeamRankingFilter>(request.RankingFilter, ignoreCase: true, out var rankingFilter))
                throw new ArgumentException($"Invalid ranking filter: {request.RankingFilter}");

            var tiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission; // Default value
            //if (!Enum.TryParse<TiebreakerTiePolicy>(request.TiebreakerTiePolicy, ignoreCase: true, out var tiebreakerTiePolicy))
            //    throw new ArgumentException($"Invalid tiebreaker tie policy: {request.TiebreakerTiePolicy}");

            // === Canonical Resolution ===
            var franchiseIds = await _canonicalDataProvider.GetConferenceIdsBySlugsAsync(
                Sport.FootballNcaa,
                request.ConferenceSlugs);

            var unresolved = request.ConferenceSlugs
                .Except(franchiseIds.Keys, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (unresolved.Any())
                throw new InvalidOperationException($"Unknown conference slugs: {string.Join(", ", unresolved)}");

            // === Build Command ===
            var command = new CreateLeagueCommand
            {
                Name = request.Name.Trim(),
                CommissionerUserId = currentUserId,
                Conferences = franchiseIds,
                CreatedBy = currentUserId,
                Description = request.Description?.Trim(),
                IsPublic = request.IsPublic,
                League = League.NCAAF,
                PickType = pickType,
                RankingFilter = rankingFilter,
                Sport = Sport.FootballNcaa,
                TiebreakerTiePolicy = tiebreakerTiePolicy,
                TiebreakerType = tiebreakerType,
                UseConfidencePoints = request.UseConfidencePoints,
            };

            return await _handler.ExecuteAsync(command, cancellationToken);
        }

    }
}
