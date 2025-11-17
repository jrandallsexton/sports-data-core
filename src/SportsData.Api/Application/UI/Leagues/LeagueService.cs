using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Previews;
using SportsData.Api.Application.UI.Contest;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.JoinLeague;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage.Dtos;
using SportsData.Api.Application.UI.Picks;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;
using SportsData.Core.Processing;

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
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public LeagueService(
            ILogger<LeagueService> logger,
            AppDataContext dbContext,
            IProvideCanonicalData canonicalDataProvider,
            ICreateLeagueCommandHandler handler,
            IJoinLeagueCommandHandler joinLeagueHandler,
            IPickService pickService,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _canonicalDataProvider = canonicalDataProvider;
            _handler = handler;
            _joinLeagueHandler = joinLeagueHandler;
            _pickService = pickService;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task<Result<Guid>> CreateAsync(
            CreateLeagueRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            // === Validation ===
            if (string.IsNullOrWhiteSpace(request.Name))
                return new Failure<Guid>(
                    default,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(request.Name), "League name is required.")]);

            // === Enum Resolution ===
            if (!Enum.TryParse<PickType>(request.PickType, ignoreCase: true, out var pickType))
                return new Failure<Guid>(
                    default,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(request.PickType), $"Invalid pick type: {request.PickType}")]);

            if (!Enum.TryParse<TiebreakerType>(request.TiebreakerType, ignoreCase: true, out var tiebreakerType))
                return new Failure<Guid>(
                    default,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(request.TiebreakerType), $"Invalid tiebreaker type: {request.TiebreakerType}")]);

            if (!Enum.TryParse<TeamRankingFilter>(request.RankingFilter, ignoreCase: true, out var rankingFilter))
                return new Failure<Guid>(
                    default,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(request.RankingFilter), $"Invalid ranking filter: {request.RankingFilter}")]);

            if (!Enum.TryParse<TiebreakerTiePolicy>(request.TiebreakerTiePolicy, ignoreCase: true, out var tiebreakerTiePolicy))
                return new Failure<Guid>(
                    default,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(request.TiebreakerTiePolicy), $"Invalid tiebreaker tie policy: {request.TiebreakerTiePolicy}")]);

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
                return new Failure<Guid>(
                    default,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(request.ConferenceSlugs), $"Unknown conference slugs: {string.Join(", ", unresolved)}")]);

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

            var leagueId = await _handler.ExecuteAsync(command, cancellationToken);
            return new Success<Guid>(leagueId);
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
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(leagueId), "League not found")]);

            if (league.Members.Any(m => m.UserId == userId))
                return new Failure<Guid?>(
                    leagueId,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(userId), "User is already a member of this league")]);

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
                    [new ValidationFailure(nameof(leagueId), "Could not join league due to an unknown error")]);
        }

        public async Task<Result<LeagueWeekMatchupsDto>> GetMatchupsForLeagueWeekAsync(
            Guid userId,
            Guid leagueId,
            int week,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "LeagueService.GetMatchupsForLeagueWeekAsync called with userId={UserId}, leagueId={LeagueId}, week={Week}", 
                userId, 
                leagueId, 
                week);

            try
            {
                _logger.LogDebug(
                    "Querying database for league, leagueId={LeagueId}", 
                    leagueId);
                
                var league = await _dbContext.PickemGroups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == leagueId, cancellationToken: cancellationToken);

                if (league is null)
                {
                    _logger.LogWarning(
                        "League not found, leagueId={LeagueId}, userId={UserId}, week={Week}", 
                        leagueId, 
                        userId, 
                        week);
                    
                    return new Failure<LeagueWeekMatchupsDto>(
                        default!,
                        ResultStatus.NotFound,
                        [new ValidationFailure(nameof(leagueId), "League not found")]);
                }

                _logger.LogInformation(
                    "League found: {LeagueName}, PickType={PickType}, leagueId={LeagueId}", 
                    league.Name, 
                    league.PickType, 
                    leagueId);

                _logger.LogDebug(
                    "Querying database for league matchups, leagueId={LeagueId}, week={Week}", 
                    leagueId, 
                    week);
                
                var matchups = await _dbContext.PickemGroupMatchups
                    .Where(x => x.GroupId == leagueId && x.SeasonWeek == week)
                    .Select(x => new LeagueWeekMatchupsDto.MatchupForPickDto
                    {
                        StartDateUtc = x.StartDateUtc,
                        ContestId = x.ContestId,
                        AwayRank = x.AwayRank,
                        HomeRank = x.HomeRank,
                    })
                    .ToListAsync(cancellationToken);

                _logger.LogInformation(
                    "Retrieved {Count} matchups from database for leagueId={LeagueId}, week={Week}", 
                    matchups.Count, 
                    leagueId, 
                    week);

                var contestIds = matchups.Select(x => x.ContestId).Distinct().ToList();

                _logger.LogDebug(
                    "Querying contest predictions for {ContestCount} contests, leagueId={LeagueId}, week={Week}", 
                    contestIds.Count, 
                    leagueId, 
                    week);
                
                var predictions = await _dbContext.ContestPredictions
                    .Where(x => contestIds.Contains(x.ContestId))
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                _logger.LogDebug(
                    "Found {PredictionCount} contest predictions, leagueId={LeagueId}, week={Week}", 
                    predictions.Count, 
                    leagueId, 
                    week);

                _logger.LogDebug(
                    "Querying matchup previews for {ContestCount} contests, leagueId={LeagueId}, week={Week}", 
                    contestIds.Count, 
                    leagueId, 
                    week);
                
                var previews = await _dbContext.MatchupPreviews
                    .Where(x => contestIds.Contains(x.ContestId) && x.RejectedUtc == null)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                _logger.LogDebug(
                    "Found {PreviewCount} matchup previews, leagueId={LeagueId}, week={Week}", 
                    previews.Count, 
                    leagueId, 
                    week);

                _logger.LogDebug(
                    "Calling CanonicalDataProvider.GetMatchupsByContestIds for {ContestCount} contests, leagueId={LeagueId}, week={Week}", 
                    contestIds.Count, 
                    leagueId, 
                    week);
                
                var canonicalMatchups = await _canonicalDataProvider.GetMatchupsByContestIds(contestIds);

                _logger.LogInformation(
                    "Received {CanonicalCount} canonical matchups from CanonicalDataProvider for leagueId={LeagueId}, week={Week}", 
                    canonicalMatchups?.Count ?? 0, 
                    leagueId, 
                    week);

                if (canonicalMatchups == null || canonicalMatchups.Count == 0)
                {
                    _logger.LogWarning(
                        "No canonical matchups returned from CanonicalDataProvider for leagueId={LeagueId}, week={Week}", 
                        leagueId, 
                        week);
                    canonicalMatchups = [];
                }

                // Create dictionary for fast lookup of canonical values
                var canonicalMap = canonicalMatchups.ToDictionary(x => x.ContestId);

                _logger.LogDebug(
                    "Enriching {MatchupCount} matchups with canonical data, leagueId={LeagueId}, week={Week}", 
                    matchups.Count, 
                    leagueId, 
                    week);

                // Fill in canonical fields for each league matchup
                foreach (var matchup in matchups)
                {
                    if (canonicalMap.TryGetValue(matchup.ContestId, out var canonical))
                    {
                        //matchup.StartDateUtc = canonical.StartDateUtc;
                        matchup.Status = canonical.Status;
                        matchup.Broadcasts = canonical.Broadcasts;

                        // Away team
                        matchup.Away = canonical.Away;
                        matchup.AwayShort = canonical.AwayShort;
                        matchup.AwayFranchiseSeasonId = canonical.AwayFranchiseSeasonId;
                        matchup.AwayLogoUri = canonical.AwayLogoUri;
                        matchup.AwaySlug = canonical.AwaySlug;
                        matchup.AwayColor = canonical.AwayColor;
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
                        matchup.HomeColor = canonical.HomeColor;
                        matchup.HomeWins = canonical.HomeWins;
                        matchup.HomeLosses = canonical.HomeLosses;
                        matchup.HomeConferenceWins = canonical.HomeConferenceWins;
                        matchup.HomeConferenceLosses = canonical.HomeConferenceLosses;
                        matchup.HomeRank = canonical.HomeRank;

                        // Odds
                        matchup.SpreadCurrent = canonical.SpreadCurrent.HasValue
                            ? Math.Round(canonical.SpreadCurrent.Value, 1, MidpointRounding.AwayFromZero)
                            : (decimal?)null;

                        matchup.SpreadOpen = canonical.SpreadOpen.HasValue
                            ? Math.Round(canonical.SpreadOpen.Value, 1, MidpointRounding.AwayFromZero)
                            : (decimal?)null;

                        matchup.OverUnderCurrent = canonical.OverUnderCurrent.HasValue
                            ? Math.Round(canonical.OverUnderCurrent.Value, 1, MidpointRounding.AwayFromZero)
                            : (decimal?)null;

                        matchup.OverUnderOpen = canonical.OverUnderOpen.HasValue
                            ? Math.Round(canonical.OverUnderOpen.Value, 1, MidpointRounding.AwayFromZero)
                            : (decimal?)null;

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

                        var preview = previews
                            .Where(x => x.ContestId == matchup.ContestId &&
                                        x.RejectedUtc == null)
                            .OrderByDescending(x => x.CreatedUtc)
                            .FirstOrDefault();

                        if (preview != null)
                        {
                            if (league.PickType == PickType.StraightUp)
                            {
                                matchup.AiWinnerFranchiseSeasonId = preview.PredictedStraightUpWinner;
                            }
                            else
                            {
                                matchup.AiWinnerFranchiseSeasonId = preview.PredictedSpreadWinner ?? preview.PredictedStraightUpWinner;
                            }
                        }

                        matchup.IsPreviewAvailable = previews.Any(x => x.ContestId == matchup.ContestId &&
                                                                       x.RejectedUtc == null);

                        matchup.IsPreviewReviewed = previews.Any(x => x.ContestId == matchup.ContestId &&
                                                                      x is { ApprovedUtc: not null, RejectedUtc: null });

                        var contestPredictions = predictions.Where(x => x.ContestId == matchup.ContestId);

                        foreach (var prediction in contestPredictions)
                        {
                            matchup.Predictions.Add(new ContestPredictionDto()
                            {
                                ContestId = prediction.ContestId,
                                ModelVersion = prediction.ModelVersion,
                                PredictionType = prediction.PredictionType,
                                WinProbability = prediction.WinProbability,
                                WinnerFranchiseSeasonId = prediction.WinnerFranchiseSeasonId
                            });
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "No canonical matchup found for ContestId={ContestId}, leagueId={LeagueId}, week={Week}", 
                            matchup.ContestId, 
                            leagueId, 
                            week);
                    }
                }

                _logger.LogDebug(
                    "Finished enriching matchups, creating result DTO for leagueId={LeagueId}, week={Week}", 
                    leagueId, 
                    week);

                var result = new LeagueWeekMatchupsDto
                {
                    PickType = league!.PickType,
                    SeasonYear = DateTime.UtcNow.Year, // Assuming current year for simplicity
                    WeekNumber = week,
                    Matchups = matchups.OrderBy(x => x.StartDateUtc).ToList()
                };

                _logger.LogInformation(
                    "Successfully completed GetMatchupsForLeagueWeekAsync for leagueId={LeagueId}, week={Week}, userId={UserId}, returning {Count} matchups", 
                    leagueId, 
                    week, 
                    userId, 
                    result.Matchups.Count);

                return new Success<LeagueWeekMatchupsDto>(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, 
                    "Error in GetMatchupsForLeagueWeekAsync for leagueId={LeagueId}, week={Week}, userId={UserId}", 
                    leagueId, 
                    week, 
                    userId);
                
                return new Failure<LeagueWeekMatchupsDto>(
                    default!,
                    ResultStatus.BadRequest,
                    [new ValidationFailure(nameof(leagueId), $"Error retrieving matchups: {ex.Message}")]);
            }
        }

        public async Task<Result<Guid>> DeleteLeague(
            Guid userId,
            Guid leagueId,
            CancellationToken cancellationToken = default)
        {
            var league = await _dbContext.PickemGroups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == leagueId, cancellationToken: cancellationToken);

            if (league is null)
                return new Failure<Guid>(
                    default,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(leagueId), $"League with ID {leagueId} not found.")]);

            if (league.CommissionerUserId != userId)
                return new Failure<Guid>(
                    default,
                    ResultStatus.Unauthorized,
                    [new ValidationFailure(nameof(userId), $"User {userId} is not the commissioner of league {leagueId}.")]);

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

            return new Success<Guid>(leagueId);
        }

        public async Task<Result<List<PublicLeagueDto>>> GetPublicLeagues(Guid userId)
        {
            var leagues = await _dbContext.PickemGroups
                .Include(g => g.CommissionerUser)
                .Include(g => g.Members)
                .Where(g => g.IsPublic && !g.Members.Any(x => x.UserId == userId))
                .ToListAsync();

            var result = leagues.Select(x => new PublicLeagueDto
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

            return new Success<List<PublicLeagueDto>>(result);
        }

        public async Task<Result<LeagueWeekOverviewDto>> GetLeagueWeekOverview(
            Guid leagueId,
            int week)
        {
            var league = await _dbContext.PickemGroups
                .AsNoTracking()
                .Include(x => x.Members)
                .FirstOrDefaultAsync(g => g.Id == leagueId);

            if (league is null)
                return new Failure<LeagueWeekOverviewDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(leagueId), $"League with ID {leagueId} not found.")]);

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
                    _logger.LogError("Matchup could not be found for contest {ContestId}", canonicalContest.ContestId);
                    return new Failure<LeagueWeekOverviewDto>(
                        default!,
                        ResultStatus.BadRequest,
                        [new ValidationFailure(nameof(canonicalContest.ContestId), "Matchup could not be found")]);
                }
                
                canonicalContest.IsLocked = canonicalContest.StartDateUtc.AddMinutes(-5) <= DateTime.UtcNow;
                canonicalContest.WinnerFranchiseSeasonId = canonicalContest.AwayScore > canonicalContest.HomeScore
                    ? canonicalContest.AwayFranchiseSeasonId
                    : canonicalContest.HomeFranchiseSeasonId;

                // Determine spread winner based on the matchup spread
                if (matchup is { AwaySpread: not null, HomeSpread: not null })
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
                var userPicksResult = await _pickService
                    .GetUserPicksByGroupAndWeek(member.UserId, leagueId, week, CancellationToken.None);

                if (userPicksResult.IsSuccess)
                {
                    result.UserPicks.AddRange(userPicksResult.Value);
                }
                else
                {
                    _logger.LogWarning("Could not retrieve user picks for user {UserId} in league {LeagueId} week {Week}", 
                        member.UserId, leagueId, week);
                }
            }

            return new Success<LeagueWeekOverviewDto>(result);
        }

        public async Task<Result<Guid>> GenerateLeagueWeekPreviews(Guid leagueId, int weekNumber)
        {
            var league = await _dbContext.PickemGroups
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == leagueId);

            if (league is null)
                return new Failure<Guid>(
                    default,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(leagueId), $"League with ID {leagueId} not found.")]);

            var contestIds = await _dbContext.PickemGroupMatchups
                .AsNoTracking()
                .Where(x => x.GroupId == leagueId && x.SeasonWeek == weekNumber)
                .Select(x => x.ContestId)
                .ToListAsync();

            foreach (var contestId in contestIds)
            {
                var previewExists = await _dbContext.MatchupPreviews
                    .Where(x => x.ContestId == contestId && x.RejectedUtc == null)
                    .AnyAsync();

                if (previewExists)
                    continue;

                var cmd = new GenerateMatchupPreviewsCommand
                {
                    ContestId = contestId
                };
                _backgroundJobProvider.Enqueue<MatchupPreviewProcessor>(p => p.Process(cmd));
            }

            return new Success<Guid>(leagueId);
        }
    }
}
