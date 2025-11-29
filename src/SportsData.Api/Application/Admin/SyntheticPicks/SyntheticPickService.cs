using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Config;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.SyntheticPicks;

/// <summary>
/// Service responsible for generating synthetic user picks with pick style logic applied.
/// </summary>
public class SyntheticPickService : ISyntheticPickService
{
    private readonly ISyntheticPickStyleProvider _pickStyleProvider;
    private readonly ILogger<SyntheticPickService> _logger;
    private readonly AppDataContext _dataContext;
    private readonly ILeagueService _leagueService;

    public SyntheticPickService(
        ISyntheticPickStyleProvider pickStyleProvider,
        ILogger<SyntheticPickService> logger,
        AppDataContext dataContext,
        ILeagueService leagueService)
    {
        _pickStyleProvider = pickStyleProvider;
        _logger = logger;
        _dataContext = dataContext;
        _leagueService = leagueService;
    }

    public async Task GenerateMetricBasedPicksForSynthetic(
        Guid pickemGroupId,
        PickType pickemGroupPickType,
        Guid syntheticId,
        string syntheticPickStyle,
        int seasonWeekNumber)
    {
        // get the matchups for the group
        var groupMatchupsResult = await _leagueService
            .GetMatchupsForLeagueWeekAsync(syntheticId, pickemGroupId, seasonWeekNumber, CancellationToken.None);

        if (!groupMatchupsResult.IsSuccess)
        {
            _logger.LogWarning("Could not get matchups for group {GroupId}", pickemGroupId);
            return;
        }

        var groupMatchups = groupMatchupsResult.Value;

        // iterate each group matchup
        foreach (var matchup in groupMatchups.Matchups)
        {
            // get the synthetic's pick
            var synPick = await _dataContext.UserPicks
                .Where(x => x.ContestId == matchup.ContestId &&
                            x.PickemGroupId == pickemGroupId &&
                            x.UserId == syntheticId)
                .FirstOrDefaultAsync();

            // do we already have one?
            if (synPick is not null)
                continue;

            // get the previously-generated ContestPrediction
            var prediction = await _dataContext.ContestPredictions
                .AsNoTracking()
                .Where(x => x.ContestId == matchup.ContestId &&
                            x.PredictionType == pickemGroupPickType)
                .OrderByDescending(x => x.CreatedUtc)
                .FirstOrDefaultAsync();

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
                    UserPickType.StraightUp : UserPickType.AgainstTheSpread,
                Week = seasonWeekNumber,
                TiebreakerType = TiebreakerType.TotalPoints
            };

            await _dataContext.UserPicks.AddAsync(synPick);
            await _dataContext.SaveChangesAsync();
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

        // Default to model's prediction
        var finalPickFranchiseId = prediction.WinnerFranchiseSeasonId;

        // Only apply threshold logic for ATS picks with a valid spread
        if (prediction.PredictionType == PickType.AgainstTheSpread && matchup.SpreadCurrent.HasValue)
        {
            var spreadAbs = Math.Abs(matchup.SpreadCurrent.Value);
            var requiredConfidence = _pickStyleProvider.GetRequiredConfidence(pickStyle, (double)spreadAbs);
            var modelConfidence = (double)prediction.WinProbability;

            // If model confidence doesn't meet threshold, flip to opposite team
            if (modelConfidence < requiredConfidence)
            {
                finalPickFranchiseId = prediction.WinnerFranchiseSeasonId == matchup.HomeFranchiseSeasonId
                    ? matchup.AwayFranchiseSeasonId
                    : matchup.HomeFranchiseSeasonId;

                _logger.LogDebug(
                    "Style '{Style}': Spread {Spread} requires {Required:P0}, model has {Actual:P0} - flipping pick for contest {ContestId}",
                    pickStyle,
                    spreadAbs,
                    requiredConfidence,
                    modelConfidence,
                    matchup.ContestId);
            }
            else
            {
                _logger.LogDebug(
                    "Style '{Style}': Spread {Spread} requires {Required:P0}, model has {Actual:P0} - keeping model prediction for contest {ContestId}",
                    pickStyle,
                    spreadAbs,
                    requiredConfidence,
                    modelConfidence,
                    matchup.ContestId);
            }
        }

        return finalPickFranchiseId;
    }
}
