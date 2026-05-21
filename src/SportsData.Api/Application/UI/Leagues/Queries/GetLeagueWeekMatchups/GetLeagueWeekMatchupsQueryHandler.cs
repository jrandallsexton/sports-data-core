using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Contest.Dtos;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.Mapping;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Infrastructure.Clients.Contest;
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
    private readonly IContestClientFactory _contestClientFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetLeagueWeekMatchupsQueryHandler(
        ILogger<GetLeagueWeekMatchupsQueryHandler> logger,
        AppDataContext dbContext,
        IContestClientFactory contestClientFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dbContext = dbContext;
        _contestClientFactory = contestClientFactory;
        _dateTimeProvider = dateTimeProvider;
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

            var groupMatchups = await _dbContext.PickemGroupMatchups
                .AsNoTracking()
                .Where(x => x.GroupId == query.LeagueId && x.SeasonWeek == query.Week)
                .Select(x => new
                {
                    x.StartDateUtc,
                    x.ContestId,
                    x.AwayRank,
                    x.HomeRank,
                    x.Headline,
                    x.SeasonYear
                })
                .ToListAsync(cancellationToken);

            var matchups = groupMatchups
                .Select(x => new LeagueWeekMatchupsDto.MatchupForPickDto
                {
                    StartDateUtc = x.StartDateUtc,
                    ContestId = x.ContestId,
                    AwayRank = x.AwayRank,
                    HomeRank = x.HomeRank,
                    HeadLine = x.Headline
                })
                .ToList();

            // Season year is authoritative on PickemGroupMatchup (set at generation
            // time). Falls back to the current UTC year (via IDateTimeProvider for
            // deterministic testing) only when a week returned zero matchups — which
            // can't cleanly infer a year from the data itself.
            var seasonYear = groupMatchups.FirstOrDefault()?.SeasonYear ?? _dateTimeProvider.UtcNow().Year;

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
                "Calling ContestClient.GetMatchupsByContestIds for {ContestCount} contests, leagueId={LeagueId}, week={Week}",
                contestIds.Count,
                query.LeagueId,
                query.Week);

            var matchupsResult = await _contestClientFactory.Resolve(league.Sport).GetMatchupsByContestIds(contestIds);
            if (!matchupsResult.IsSuccess)
            {
                _logger.LogError("Failed to retrieve canonical matchups for leagueId={LeagueId}, week={Week}", query.LeagueId, query.Week);
                return new Failure<LeagueWeekMatchupsDto>(
                    default!,
                    ResultStatus.Error,
                    [new FluentValidation.Results.ValidationFailure("matchups", "Failed to retrieve matchup data from Producer")]);
            }
            var canonicalMatchups = matchupsResult.Value;

            _logger.LogInformation(
                "Received {CanonicalCount} canonical matchups from ContestClient for leagueId={LeagueId}, week={Week}",
                canonicalMatchups?.Count ?? 0,
                query.LeagueId,
                query.Week);

            if (canonicalMatchups == null || canonicalMatchups.Count == 0)
            {
                _logger.LogWarning(
                    "No canonical matchups returned from ContestClient for leagueId={LeagueId}, week={Week}",
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
                    // Canonical fields (teams, odds, scores, status, probables,
                    // streaming, etc.) — extracted to MatchupForPickDtoMapper so
                    // the admin debug endpoint can reuse the same shape without
                    // a league context. League-context fields (HeadLine,
                    // Predictions, AiWinner, IsPreview*) stay below.
                    MatchupForPickDtoMapper.ApplyCanonical(matchup, canonical);

                    // Headline priority: live CompetitionNote.Headline (marquee
                    // tag — bowl/conf championship/postseason designation) wins,
                    // baseball CurrentSeriesSummary is the regular-season fallback
                    // (e.g. "BOS leads series 2-0"), frozen PickemGroupMatchup
                    // value (already on matchup.HeadLine from the initial
                    // projection) is the last-resort safety net for historical
                    // leagues whose CompetitionNote may no longer resolve.
                    matchup.HeadLine = canonical.Headline
                                       ?? canonical.CurrentSeriesSummary
                                       ?? matchup.HeadLine;

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
                SeasonYear = seasonYear,
                WeekNumber = query.Week,
                Sport = league.Sport.ToString(),
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
