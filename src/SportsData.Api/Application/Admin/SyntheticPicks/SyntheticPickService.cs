using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Config;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.Admin.SyntheticPicks;

/// <summary>
/// Service responsible for generating synthetic user picks with pick style logic applied.
/// </summary>
public class SyntheticPickService : ISyntheticPickService
{
    private readonly ISyntheticPickStyleProvider _pickStyleProvider;
    private readonly ILogger<SyntheticPickService> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IGetLeagueWeekMatchupsQueryHandler _getLeagueWeekMatchupsHandler;

    public SyntheticPickService(
        ISyntheticPickStyleProvider pickStyleProvider,
        ILogger<SyntheticPickService> logger,
        AppDataContext dataContext,
        IGetLeagueWeekMatchupsQueryHandler getLeagueWeekMatchupsHandler)
    {
        _pickStyleProvider = pickStyleProvider;
        _logger = logger;
        _dataContext = dataContext;
        _getLeagueWeekMatchupsHandler = getLeagueWeekMatchupsHandler;
    }

    /// <summary>
    /// Generates metric-based synthetic picks for a synthetic user in a pick'em group for a specific week by applying the configured pick style thresholds to existing contest predictions.
    /// </summary>
    /// <param name="pickemGroupId">The pick'em group (league) identifier to generate picks for.</param>
    /// <param name="pickemGroupPickType">The prediction type (StraightUp or AgainstTheSpread) to use when selecting predictions.</param>
    /// <param name="syntheticId">The synthetic user's identifier for which picks will be created.</param>
    /// <param name="syntheticPickStyle">The pick style key used to obtain required confidence thresholds when evaluating against-the-spread predictions.</param>
    /// <param name="seasonWeekNumber">The season week number whose matchups and predictions will be used to generate picks.</param>
    /// <param name="cancellationToken">Cancellation token to observe while performing database and handler operations.</param>
    /// <remarks>
    /// For each matchup in the group, the method:
    /// - Skips if the synthetic already has a pick for the contest.
    /// - Uses the most recent matching ContestPrediction to determine a pick via DeterminePickWithThreshold.
    /// - Adds a new PickemGroupUserPick when appropriate and saves all new picks in a single batch.
    /// If retrieving matchups fails, the method logs a warning and exits without creating picks.
    /// </remarks>
    public async Task GenerateMetricBasedPicksForSynthetic(
        Guid pickemGroupId,
        PickType pickemGroupPickType,
        Guid syntheticId,
        string syntheticPickStyle,
        int seasonWeekNumber,
        CancellationToken cancellationToken = default)
    {
        // get the matchups for the group
        var query = new GetLeagueWeekMatchupsQuery
        {
            UserId = syntheticId,
            LeagueId = pickemGroupId,
            Week = seasonWeekNumber
        };
        var groupMatchupsResult = await _getLeagueWeekMatchupsHandler.ExecuteAsync(query, cancellationToken);

        if (!groupMatchupsResult.IsSuccess)
        {
            _logger.LogWarning("Could not get matchups for group {GroupId}", pickemGroupId);
            return;
        }

        var groupMatchups = groupMatchupsResult.Value;
        var picksAdded = 0;

        // iterate each group matchup
        foreach (var matchup in groupMatchups.Matchups)
        {
            // get the synthetic's pick
            var synPick = await _dataContext.UserPicks
                .Where(x => x.ContestId == matchup.ContestId &&
                            x.PickemGroupId == pickemGroupId &&
                            x.UserId == syntheticId)
                .FirstOrDefaultAsync(cancellationToken);

            // do we already have one?
            if (synPick is not null)
                continue;

            // get the previously-generated ContestPrediction
            var prediction = await _dataContext.ContestPredictions
                .AsNoTracking()
                .Where(x => x.ContestId == matchup.ContestId &&
                            x.PredictionType == pickemGroupPickType)
                .OrderByDescending(x => x.CreatedUtc)
                .FirstOrDefaultAsync(cancellationToken);

            // no prediction? skip it
            if (prediction is null)
                continue;

            // Apply pick style thresholds to determine final pick
            var finalPickFranchiseId = DeterminePickWithThreshold(
                prediction,
                matchup,
                syntheticPickStyle);

            // generate the synthetic's pick from the ContestPrediction
            synPick = new PickemGroupUserPick()
            {
                UserId = syntheticId,
                ContestId = matchup.ContestId,
                CreatedUtc = prediction.CreatedUtc,
                CreatedBy = syntheticId,
                FranchiseId = finalPickFranchiseId,
                PickemGroupId = pickemGroupId,
                PickType = prediction.PredictionType == PickType.StraightUp ?
                    PickType.StraightUp : PickType.AgainstTheSpread,
                Week = seasonWeekNumber,
                TiebreakerType = TiebreakerType.TotalPoints
            };

            await _dataContext.UserPicks.AddAsync(synPick, cancellationToken);
            picksAdded++;
        }

        // Batch save all picks for this synthetic in this group
        if (picksAdded > 0)
        {
            await _dataContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Created {count} metric-based picks for synthetic {syntheticId} in group {groupId}", 
                picksAdded, syntheticId, pickemGroupId);
        }
    }

    private Guid DeterminePickWithThreshold(
        ContestPrediction prediction,
        LeagueWeekMatchupsDto.MatchupForPickDto matchup,
        string pickStyle)
    {
        if (prediction == null)
            throw new ArgumentNullException(nameof(prediction));
        
        if (matchup == null)
            throw new ArgumentNullException(nameof(matchup));
        
        if (string.IsNullOrWhiteSpace(pickStyle))
            throw new ArgumentException("Pick style cannot be null or empty", nameof(pickStyle));

        // For straight up picks, always use the model's prediction
        if (prediction.PredictionType == PickType.StraightUp)
        {
            return prediction.WinnerFranchiseSeasonId;
        }

        // For ATS picks without a spread, fall back to the model's prediction
        if (!matchup.SpreadCurrent.HasValue)
        {
            return prediction.WinnerFranchiseSeasonId;
        }

        // ATS logic with threshold
        // IMPORTANT: ContestPrediction is ALWAYS relative to the HOME team
        // - WinnerFranchiseSeasonId should be the home team's FranchiseSeasonId
        // - WinProbability is the home team's probability to cover the spread
        // - Spread is always relative to home team (negative = home favored)
        
        var spreadAbs = Math.Abs(matchup.SpreadCurrent.Value);
        var requiredConfidence = _pickStyleProvider.GetRequiredConfidence(pickStyle, (double)spreadAbs);
        var homeTeamCoverProbability = (double)prediction.WinProbability;
        
        // Determine which team is the favorite based on spread
        // Negative spread = home is favorite, Positive spread = away is favorite
        var favoriteTeam = matchup.SpreadCurrent.Value < 0 
            ? matchup.HomeFranchiseSeasonId 
            : matchup.AwayFranchiseSeasonId;
        
        var underdogTeam = favoriteTeam == matchup.HomeFranchiseSeasonId
            ? matchup.AwayFranchiseSeasonId
            : matchup.HomeFranchiseSeasonId;
        
        // Calculate favorite's probability to cover
        // If home is favorite: use home probability directly
        // If away is favorite: use inverse of home probability
        var favoriteIsHome = matchup.SpreadCurrent.Value < 0;
        var favoriteCoverProbability = favoriteIsHome 
            ? homeTeamCoverProbability 
            : (1.0 - homeTeamCoverProbability);
        
        // Apply threshold: favorite must meet confidence threshold to pick them
        if (favoriteCoverProbability >= requiredConfidence)
        {
            // Favorite meets threshold - pick the favorite
            _logger.LogDebug(
                "Style '{Style}': Spread {Spread} requires {Required:P0}, favorite has {FavConf:P0} - picking favorite ({Team}) [MEETS THRESHOLD] for contest {ContestId}",
                pickStyle,
                spreadAbs,
                requiredConfidence,
                favoriteCoverProbability,
                favoriteTeam,
                matchup.ContestId);
            
            return favoriteTeam;
        }
        else
        {
            // Favorite doesn't meet threshold - pick the underdog
            var underdogCoverProbability = 1.0 - favoriteCoverProbability;
            _logger.LogDebug(
                "Style '{Style}': Spread {Spread} requires {Required:P0}, favorite only has {FavConf:P0} (underdog: {DogConf:P0}) - picking underdog ({Team}) [BELOW THRESHOLD] for contest {ContestId}",
                pickStyle,
                spreadAbs,
                requiredConfidence,
                favoriteCoverProbability,
                underdogCoverProbability,
                underdogTeam,
                matchup.ContestId);
            
            return underdogTeam;
        }
    }
}