using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.JoinLeague;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage.Dtos;
using SportsData.Api.Application.UI.Picks;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues
{
    public class LeagueService : ILeagueService
    {
        private readonly ILogger<LeagueService> _logger;
        private readonly AppDataContext _dbContext;
        private readonly IProvideCanonicalData _canonicalDataProvider;
        private readonly ICreateLeagueCommandHandler _handler;
        private readonly IJoinLeagueCommandHandler _joinLeagueHandler;
        private readonly IPickService _pickService;

        public LeagueService(
            ILogger<LeagueService> logger,
            AppDataContext dbContext,
            IProvideCanonicalData canonicalDataProvider,
            ICreateLeagueCommandHandler handler,
            IJoinLeagueCommandHandler joinLeagueHandler,
            IPickService pickService)
        {
            _logger = logger;
            _dbContext = dbContext;
            _canonicalDataProvider = canonicalDataProvider;
            _handler = handler;
            _joinLeagueHandler = joinLeagueHandler;
            _pickService = pickService;
        }

        public async Task<Guid> CreateAsync(
            CreateLeagueRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            // === Validation ===
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("League name is required.");

            // === Enum Resolution ===
            if (!Enum.TryParse<PickType>(request.PickType, ignoreCase: true, out var pickType))
                throw new ArgumentException($"Invalid pick type: {request.PickType}");

            if (!Enum.TryParse<TiebreakerType>(request.TiebreakerType, ignoreCase: true, out var tiebreakerType))
                throw new ArgumentException($"Invalid tiebreaker type: {request.TiebreakerType}");

            if (!Enum.TryParse<TeamRankingFilter>(request.RankingFilter, ignoreCase: true, out var rankingFilter))
                throw new ArgumentException($"Invalid ranking filter: {request.RankingFilter}");

            if (!Enum.TryParse<TiebreakerTiePolicy>(request.TiebreakerTiePolicy, ignoreCase: true, out var tiebreakerTiePolicy))
                throw new ArgumentException($"Invalid tiebreaker tie policy: {request.TiebreakerTiePolicy}");

            // === Canonical Resolution ===
            var conferenceIds = request.ConferenceSlugs.Count > 0
                ? await _canonicalDataProvider.GetConferenceIdsBySlugsAsync(
                    Sport.FootballNcaa,
                    2025, // TODO: Replace with dynamic year
                    request.ConferenceSlugs)
                : new Dictionary<Guid, string>();

            var unresolved = request.ConferenceSlugs
                .Except(conferenceIds.Values, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (unresolved.Any())
                throw new InvalidOperationException($"Unknown conference slugs: {string.Join(", ", unresolved)}");

            // === Build Command ===
            var command = new CreateLeagueCommand
            {
                Name = request.Name.Trim(),
                CommissionerUserId = currentUserId,
                Conferences = conferenceIds,
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

            var contestIds = matchups.Select(x => x.ContestId).Distinct().ToList();

            var previews = await _dbContext.MatchupPreviews
                .Where(x => contestIds.Contains(x.ContestId))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

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

                    // Result
                    matchup.IsComplete = canonical.CompletedUtc.HasValue;
                    matchup.AwayScore = canonical.AwayScore;
                    matchup.HomeScore = canonical.HomeScore;
                    matchup.WinnerFranchiseSeasonId = canonical.WinnerFranchiseSeasonId;
                    matchup.SpreadWinnerFranchiseSeasonId = canonical.SpreadWinnerFranchiseSeasonId;
                    matchup.OverUnderResult = canonical.OverUnderResult;
                    matchup.CompletedUtc = canonical.CompletedUtc;

                    matchup.IsPreviewAvailable = previews.Any(x => x.ContestId == matchup.ContestId &&
                                                                   x.RejectedUtc == null);

                    matchup.IsPreviewReviewed = previews.Any(x => x.ContestId == matchup.ContestId &&
                                                                  x is { ApprovedUtc: not null, RejectedUtc: null });
                }
            }

            var league = await _dbContext.PickemGroups
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == leagueId, cancellationToken: cancellationToken);

            return new LeagueWeekMatchupsDto
            {
                PickType = league!.PickType,
                SeasonYear = DateTime.UtcNow.Year, // Assuming current year for simplicity
                WeekNumber = week,
                Matchups = matchups.OrderBy(x => x.StartDateUtc).ToList()
            };
        }

        public async Task<Guid> DeleteLeague(
            Guid userId,
            Guid leagueId,
            CancellationToken cancellationToken = default)
        {
            // ensure the user is the commissioner of the league

            var league = await _dbContext.PickemGroups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == leagueId, cancellationToken: cancellationToken);

            if (league is null)
                throw new InvalidOperationException($"League with ID {leagueId} not found.");

            if (league.CommissionerUserId != userId)
                throw new InvalidOperationException($"User {userId} is not the commissioner of league {leagueId}.");

            // Remove all members
            _dbContext.PickemGroupMembers.RemoveRange(league.Members);

            // Remove all picks
            _dbContext.UserPicks.RemoveRange(
                _dbContext.UserPicks.Where(p => p.PickemGroupId == leagueId));

            // Remove all matchups
            _dbContext.PickemGroupMatchups.RemoveRange(
                _dbContext.PickemGroupMatchups.Where(m => m.GroupId == leagueId));

            // Remove the league itself
            _dbContext.PickemGroups.Remove(league);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return leagueId;
        }

        public async Task<List<PublicLeagueDto>> GetPublicLeagues(Guid userId)
        {
            var leagues = await _dbContext.PickemGroups
                .Include(g => g.CommissionerUser)
                .Include(g => g.Members)
                .Where(g => g.IsPublic && !g.Members.Any(x => x.UserId == userId))
                .ToListAsync();

            return leagues.Select(x => new PublicLeagueDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description ?? string.Empty,
                Commissioner = x.CommissionerUser.DisplayName,
                RankingFilter = (int?)x.RankingFilter ?? 0,
                PickType = (int)x.PickType,
                UseConfidencePoints = x.UseConfidencePoints,
                DropLowWeeksCount = x.DropLowWeeksCount ?? 0
            }).ToList();
        }

        public async Task<LeagueWeekOverviewDto> GetLeagueWeekOverview(
            Guid leagueId,
            int week)
        {
            var league = await _dbContext.PickemGroups
                .AsNoTracking()
                .Include(x => x.Members)
                .FirstOrDefaultAsync(g => g.Id == leagueId);

            if (league is null)
                throw new InvalidOperationException($"League with ID {leagueId} not found.");

            var matchups = await _dbContext.PickemGroupMatchups
                .AsNoTracking()
                .Where(m => m.GroupId == leagueId && m.SeasonWeek == week)
                .ToListAsync();

            var contestIds = matchups
                .Select(m => m.ContestId)
                .ToList();

            var result = new LeagueWeekOverviewDto();

            var canonicalContests = await _canonicalDataProvider
                .GetContestResultsByContestIds(contestIds);

            // once again, our canonical results have spread results
            // against the closing spread ...
            // while our matchups used a snapshot
            // how to reconcile here?

            foreach (var canonicalContest in canonicalContests)
            {
                var matchup = matchups
                    .FirstOrDefault(m => m.ContestId == canonicalContest.ContestId);

                if (matchup is null)
                {
                    _logger.LogError("Matchup could not be found");
                    throw new Exception("Matchup could not be found");
                }

                canonicalContest.AwaySpread = (decimal?)matchup.AwaySpread;
                canonicalContest.HomeSpread = (decimal?)matchup.HomeSpread;
                canonicalContest.WinnerFranchiseSeasonId = canonicalContest.AwayScore > canonicalContest.HomeScore
                    ? canonicalContest.AwayFranchiseSeasonId
                    : canonicalContest.HomeFranchiseSeasonId;

                // Determine spread winner based on the matchup spread
                if (matchup.AwaySpread.HasValue && matchup.HomeSpread.HasValue)
                {
                    var spreadDifference = (canonicalContest.AwayScore + matchup.AwaySpread.Value) - canonicalContest.HomeScore;
                    if (spreadDifference > 0)
                        canonicalContest.SpreadWinnerFranchiseSeasonId = canonicalContest.AwayFranchiseSeasonId;
                    else if (spreadDifference < 0)
                        canonicalContest.SpreadWinnerFranchiseSeasonId = canonicalContest.HomeFranchiseSeasonId;
                    else
                        canonicalContest.SpreadWinnerFranchiseSeasonId = null; // Push
                }
                else
                {
                    canonicalContest.SpreadWinnerFranchiseSeasonId = null; // No spread
                }
            }

            result.Contests = canonicalContests.OrderBy(x => x.StartDateUtc)
                .Select(x => new LeagueWeekMatchupResultDto(x)
                {
                    LeagueWinnerFranchiseSeasonId = x.SpreadWinnerFranchiseSeasonId ?? x.WinnerFranchiseSeasonId
                }).ToList();

            foreach (var member in league.Members.OrderBy(x => x.UserId))
            {
                var userPicks = await _pickService
                    .GetUserPicksByGroupAndWeek(member.UserId, leagueId, week, CancellationToken.None);

                result.UserPicks.AddRange(userPicks);
            }

            return result;
        }
    }
}
