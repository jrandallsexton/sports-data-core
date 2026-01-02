using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Contest.Dtos;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;

public interface IGetLeagueWeekMatchupsQueryHandler
{
    Task<Result<LeagueWeekMatchupsDto>> ExecuteAsync(
        GetLeagueWeekMatchupsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetLeagueWeekMatchupsQueryHandler : IGetLeagueWeekMatchupsQueryHandler
{
    private readonly ILogger<GetLeagueWeekMatchupsQueryHandler> _logger;
    private readonly AppDataContext _dbContext;
    private readonly IProvideCanonicalData _canonicalDataProvider;

    public GetLeagueWeekMatchupsQueryHandler(
        ILogger<GetLeagueWeekMatchupsQueryHandler> logger,
        AppDataContext dbContext,
        IProvideCanonicalData canonicalDataProvider)
    {
        _logger = logger;
        _dbContext = dbContext;
        _canonicalDataProvider = canonicalDataProvider;
    }

    public async Task<Result<LeagueWeekMatchupsDto>> ExecuteAsync(
        GetLeagueWeekMatchupsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetLeagueWeekMatchupsQueryHandler.ExecuteAsync called with userId={UserId}, leagueId={LeagueId}, week={Week}",
            query.UserId,
            query.LeagueId,
            query.Week);

        try
        {
            _logger.LogDebug(
                "Querying database for league, leagueId={LeagueId}",
                query.LeagueId);

            var league = await _dbContext.PickemGroups
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == query.LeagueId, cancellationToken: cancellationToken);

            if (league is null)
            {
                _logger.LogWarning(
                    "League not found, leagueId={LeagueId}, userId={UserId}, week={Week}",
                    query.LeagueId,
                    query.UserId,
                    query.Week);

                return new Failure<LeagueWeekMatchupsDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(query.LeagueId), "League not found")]);
            }

            _logger.LogInformation(
                "League found: {LeagueName}, PickType={PickType}, leagueId={LeagueId}",
                league.Name,
                league.PickType,
                query.LeagueId);

            _logger.LogDebug(
                "Querying database for league matchups, leagueId={LeagueId}, week={Week}",
                query.LeagueId,
                query.Week);

            var matchups = await _dbContext.PickemGroupMatchups
                .Where(x => x.GroupId == query.LeagueId && x.SeasonWeek == query.Week)
                .Select(x => new LeagueWeekMatchupsDto.MatchupForPickDto
                {
                    StartDateUtc = x.StartDateUtc,
                    ContestId = x.ContestId,
                    AwayRank = x.AwayRank,
                    HomeRank = x.HomeRank,
                    HeadLine = x.Headline
                })
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Retrieved {Count} matchups from database for leagueId={LeagueId}, week={Week}",
                matchups.Count,
                query.LeagueId,
                query.Week);

            var contestIds = matchups.Select(x => x.ContestId).Distinct().ToList();

            _logger.LogDebug(
                "Querying contest predictions for {ContestCount} contests, leagueId={LeagueId}, week={Week}",
                contestIds.Count,
                query.LeagueId,
                query.Week);

            var predictions = await _dbContext.ContestPredictions
                .Where(x => contestIds.Contains(x.ContestId))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            _logger.LogDebug(
                "Found {PredictionCount} contest predictions, leagueId={LeagueId}, week={Week}",
                predictions.Count,
                query.LeagueId,
                query.Week);

            _logger.LogDebug(
                "Querying matchup previews for {ContestCount} contests, leagueId={LeagueId}, week={Week}",
                contestIds.Count,
                query.LeagueId,
                query.Week);

            var previews = await _dbContext.MatchupPreviews
                .Where(x => contestIds.Contains(x.ContestId) && x.RejectedUtc == null)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            _logger.LogDebug(
                "Found {PreviewCount} matchup previews, leagueId={LeagueId}, week={Week}",
                previews.Count,
                query.LeagueId,
                query.Week);

            _logger.LogDebug(
                "Calling CanonicalDataProvider.GetMatchupsByContestIds for {ContestCount} contests, leagueId={LeagueId}, week={Week}",
                contestIds.Count,
                query.LeagueId,
                query.Week);

            var canonicalMatchups = await _canonicalDataProvider.GetMatchupsByContestIds(contestIds);

            _logger.LogInformation(
                "Received {CanonicalCount} canonical matchups from CanonicalDataProvider for leagueId={LeagueId}, week={Week}",
                canonicalMatchups?.Count ?? 0,
                query.LeagueId,
                query.Week);

            if (canonicalMatchups == null || canonicalMatchups.Count == 0)
            {
                _logger.LogWarning(
                    "No canonical matchups returned from CanonicalDataProvider for leagueId={LeagueId}, week={Week}",
                    query.LeagueId,
                    query.Week);
                canonicalMatchups = [];
            }

            // Create dictionary for fast lookup of canonical values
            var canonicalMap = canonicalMatchups.ToDictionary(x => x.ContestId);

            _logger.LogDebug(
                "Enriching {MatchupCount} matchups with canonical data, leagueId={LeagueId}, week={Week}",
                matchups.Count,
                query.LeagueId,
                query.Week);

            // Fill in canonical fields for each league matchup
            foreach (var matchup in matchups)
            {
                if (canonicalMap.TryGetValue(matchup.ContestId, out var canonical))
                {
                    matchup.Status = canonical.Status;
                    matchup.Broadcasts = canonical.Broadcasts;
                    matchup.HeadLine = canonical.HeadLine ?? matchup.HeadLine;

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
                        query.LeagueId,
                        query.Week);
                }
            }

            _logger.LogDebug(
                "Finished enriching matchups, creating result DTO for leagueId={LeagueId}, week={Week}",
                query.LeagueId,
                query.Week);

            var result = new LeagueWeekMatchupsDto
            {
                PickType = league!.PickType,
                UseConfidencePoints = league!.UseConfidencePoints,
                SeasonYear = DateTime.UtcNow.Year, // Assuming current year for simplicity
                WeekNumber = query.Week,
                Matchups = matchups.OrderBy(x => x.StartDateUtc).ToList()
            };

            _logger.LogInformation(
                "Successfully completed GetLeagueWeekMatchupsQueryHandler.ExecuteAsync for leagueId={LeagueId}, week={Week}, userId={UserId}, returning {Count} matchups",
                query.LeagueId,
                query.Week,
                query.UserId,
                result.Matchups.Count);

            return new Success<LeagueWeekMatchupsDto>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error in GetLeagueWeekMatchupsQueryHandler.ExecuteAsync for leagueId={LeagueId}, week={Week}, userId={UserId}",
                query.LeagueId,
                query.Week,
                query.UserId);

            return new Failure<LeagueWeekMatchupsDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure(nameof(query.LeagueId), $"Error retrieving matchups: {ex.Message}")]);
        }
    }
}
