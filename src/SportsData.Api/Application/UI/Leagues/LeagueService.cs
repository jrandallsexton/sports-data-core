using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.JoinLeague;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage.Dtos;
using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

using static SportsData.Api.Application.UI.Leagues.Dtos.LeagueWeekMatchupsDto;

namespace SportsData.Api.Application.UI.Leagues
{
    public interface ILeagueService
    {
        Task<Guid> CreateAsync(CreateLeagueRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
        Task<Result<Guid?>> JoinLeague(Guid leagueId, Guid userId, CancellationToken cancellationToken = default);
        Task<LeagueWeekMatchupsDto> GetMatchupsForLeagueWeekAsync(Guid leagueId, int week, CancellationToken cancellationToken = default);

    }

    public class LeagueService : ILeagueService
    {
        private readonly AppDataContext _dbContext;
        private readonly IProvideCanonicalData _canonicalDataProvider;
        private readonly ICreateLeagueCommandHandler _handler;
        private readonly IJoinLeagueCommandHandler _joinLeagueHandler;

        public LeagueService(
            AppDataContext dbContext,
            IProvideCanonicalData canonicalDataProvider,
            ICreateLeagueCommandHandler handler,
            IJoinLeagueCommandHandler joinLeagueHandler)
        {
            _dbContext = dbContext;
            _canonicalDataProvider = canonicalDataProvider;
            _handler = handler;
            _joinLeagueHandler = joinLeagueHandler;
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
                DropLowWeeksCount = request.DropLowWeeksCount
            };

            return await _handler.ExecuteAsync(command, cancellationToken);
        }

        public async Task<Result<Guid?>> JoinLeague(
            Guid leagueId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var league = await _dbContext.PickemGroups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == leagueId, cancellationToken: cancellationToken);

            if (league is null)
                return new Failure<Guid?>(
                    leagueId,
                    ResultStatus.BadRequest,
                    [new ValidationFailure(nameof(leagueId), "League Not Found")]
                );

            if (league.Members.Any(m => m.UserId == userId))
                return new Failure<Guid?>(
                    leagueId,
                    ResultStatus.BadRequest,
                    [new ValidationFailure(nameof(leagueId), "User is already a member of this league")]
                );

            var command = new JoinLeagueCommand()
            {
                PickemGroupId = leagueId,
                UserId = userId
            };

            var result = await _joinLeagueHandler.HandleAsync(command, cancellationToken);

            if (result.HasValue)
                return new Success<Guid?>(result);
            else
                return new Failure<Guid?>(
                    leagueId,
                    ResultStatus.BadRequest,
                    [new ValidationFailure(nameof(leagueId), "Could not join league due to an unknown error")]
                );
        }

        public Task<LeagueWeekMatchupsDto> GetMatchupsForLeagueWeekAsync(Guid leagueId, int week, CancellationToken cancellationToken = default)
        {
            // NOTE: These values are mocked and will be replaced once sourcing is live.
            var dto = new LeagueWeekMatchupsDto
            {
                SeasonYear = 2025,
                WeekNumber = week,
                Matchups =
                [
                    new MatchupForPickDto
            {
                ContestId = Guid.NewGuid(),
                StartDateUtc = DateTime.UtcNow.AddDays(2),

                Away = "LSU",
                AwaySlug = "lsu-tigers",
                AwayRank = 12,
                AwayWins = 5,
                AwayLosses = 2,
                AwayConferenceWins = 3,
                AwayConferenceLosses = 1,
                AwayLogoUri = new Uri("https://a.espncdn.com/i/teamlogos/ncaa/500/99.png"),

                Home = "Alabama",
                HomeSlug = "alabama-crimson-tide",
                HomeRank = 5,
                HomeWins = 6,
                HomeLosses = 1,
                HomeConferenceWins = 4,
                HomeConferenceLosses = 0,
                HomeLogoUri = new Uri("https://a.espncdn.com/i/teamlogos/ncaa/500/333.png"),

                AwaySpread = 7.5m,
                HomeSpread = -7.5m,
                OverUnder = 56.5m,

                Venue = "Bryant–Denny Stadium",
                VenueCity = "Tuscaloosa",
                VenueState = "AL"
            },
            new MatchupForPickDto
            {
                ContestId = Guid.NewGuid(),
                StartDateUtc = DateTime.UtcNow.AddDays(3),

                Away = "Texas",
                AwaySlug = "texas-longhorns",
                AwayRank = 7,
                AwayWins = 5,
                AwayLosses = 2,
                AwayConferenceWins = 2,
                AwayConferenceLosses = 2,
                AwayLogoUri = new Uri("https://a.espncdn.com/i/teamlogos/ncaa/500/251.png"),

                Home = "Oklahoma",
                HomeSlug = "oklahoma-sooners",
                HomeRank = 8,
                HomeWins = 6,
                HomeLosses = 1,
                HomeConferenceWins = 3,
                HomeConferenceLosses = 1,
                HomeLogoUri = new Uri("https://a.espncdn.com/i/teamlogos/ncaa/500/201.png"),

                AwaySpread = 2.5m,
                HomeSpread = -2.5m,
                OverUnder = 61.0m,

                Venue = "Cotton Bowl",
                VenueCity = "Dallas",
                VenueState = "TX"
            }
                ]
            };

            return Task.FromResult(dto);
        }


    }
}
