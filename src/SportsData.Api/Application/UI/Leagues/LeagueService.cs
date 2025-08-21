using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.JoinLeague;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage.Dtos;
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
        Task<LeagueWeekMatchupsDto> GetMatchupsForLeagueWeekAsync(Guid userId, Guid leagueId, int week, CancellationToken cancellationToken = default);

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

        public async Task<LeagueWeekMatchupsDto> GetMatchupsForLeagueWeekAsync(
            Guid userId,
            Guid leagueId,
            int week,
            CancellationToken cancellationToken = default)
        {
            var matchups = await _dbContext.PickemGroupMatchups
                .Where(x => x.GroupId == leagueId && x.SeasonWeek == week)
                .Select(x => new LeagueWeekMatchupsDto.MatchupForPickDto
                {
                    ContestId = x.ContestId,
                    AwaySpread = (decimal?)x.AwaySpread,
                    AwayRank = x.AwayRank,
                    HomeSpread = (decimal?)x.HomeSpread,
                    HomeRank = x.HomeRank,
                    OverUnder = (decimal?)x.OverUnder
                })
                .ToListAsync(cancellationToken);

            var contestIds = matchups.Select(x => x.ContestId).ToList();

            var canonicalMatchups = await _canonicalDataProvider.GetMatchupsByContestIds(contestIds);

            // Create dictionary for fast lookup of canonical values
            var canonicalMap = canonicalMatchups.ToDictionary(x => x.ContestId);

            // Fill in canonical fields for each league matchup
            foreach (var matchup in matchups)
            {
                if (canonicalMap.TryGetValue(matchup.ContestId, out var canonical))
                {
                    matchup.StartDateUtc = canonical.StartDateUtc;

                    // Away team
                    matchup.Away = canonical.Away;
                    matchup.AwayShort = canonical.AwayShort;
                    matchup.AwayFranchiseSeasonId = canonical.AwayFranchiseSeasonId;
                    matchup.AwayLogoUri = canonical.AwayLogoUri;
                    matchup.AwaySlug = canonical.AwaySlug;
                    matchup.AwayWins = canonical.AwayWins;
                    matchup.AwayLosses = canonical.AwayLosses;
                    matchup.AwayConferenceWins = canonical.AwayConferenceWins;
                    matchup.AwayConferenceLosses = canonical.AwayConferenceLosses;
                    matchup.AwayRank = canonical.AwayRank;

                    // Home team
                    matchup.Home = canonical.Home;
                    matchup.HomeShort = canonical.HomeShort;
                    matchup.HomeFranchiseSeasonId = canonical.HomeFranchiseSeasonId;
                    matchup.HomeLogoUri = canonical.HomeLogoUri;
                    matchup.HomeSlug = canonical.HomeSlug;
                    matchup.HomeWins = canonical.HomeWins;
                    matchup.HomeLosses = canonical.HomeLosses;
                    matchup.HomeConferenceWins = canonical.HomeConferenceWins;
                    matchup.HomeConferenceLosses = canonical.HomeConferenceLosses;
                    matchup.HomeRank = canonical.HomeRank;

                    // Venue
                    matchup.Venue = canonical.Venue;
                    matchup.VenueCity = canonical.VenueCity;
                    matchup.VenueState = canonical.VenueState;
                }
            }

            return new LeagueWeekMatchupsDto
            {
                SeasonYear = DateTime.UtcNow.Year, // Assuming current year for simplicity
                WeekNumber = week,
                Matchups = matchups.OrderBy(x => x.StartDateUtc).ToList()
            };
        }

    }
}
