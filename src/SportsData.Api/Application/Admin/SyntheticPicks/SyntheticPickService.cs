using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Config;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;

using SportsData.Api.Application.Common.Enums;

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
    private readonly IGetLeagueWeekMatchupsQueryHandler _getLeagueWeekMatchupsHandler;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SyntheticPickService(
        ISyntheticPickStyleProvider pickStyleProvider,
        ILogger<SyntheticPickService> logger,
        AppDataContext dataContext,
        IGetLeagueWeekMatchupsQueryHandler getLeagueWeekMatchupsHandler,
        IDateTimeProvider dateTimeProvider)
    {
        _pickStyleProvider = pickStyleProvider;
        _logger = logger;
        _dataContext = dataContext;
        _getLeagueWeekMatchupsHandler = getLeagueWeekMatchupsHandler;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlySet<Guid>> GenerateMetricBasedPicksForSynthetic(
        Guid pickemGroupId,
        PickType pickemGroupPickType,
        Guid syntheticId,
        string? syntheticPickStyle,
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
        var written = new HashSet<Guid>();
        var insertedPickIds = new HashSet<Guid>();
        var groupMatchupsResult = await _getLeagueWeekMatchupsHandler.ExecuteAsync(query, cancellationToken);

        if (!groupMatchupsResult.IsSuccess)
        {
            _logger.LogWarning(
                "Metric picks skipped — could not get matchups. GroupId={GroupId}, SyntheticId={SyntheticId}, Week={Week}, Status={Status}",
                pickemGroupId, syntheticId, seasonWeekNumber, groupMatchupsResult.Status);
            return written;
        }

        var groupMatchups = groupMatchupsResult.Value;
        var picksAdded = 0;
        var alreadyHad = 0;
        var noPrediction = 0;

        if (groupMatchups.Matchups.Count == 0)
        {
            // Not an error — a league with no slate for this week is normal —
            // but it is the difference between "nothing to do" and "something
            // is wrong", which the caller could not previously tell apart.
            _logger.LogInformation(
                "Metric picks: no matchups in scope. GroupId={GroupId}, SyntheticId={SyntheticId}, Week={Week}",
                pickemGroupId, syntheticId, seasonWeekNumber);
            return written;
        }

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
            {
                alreadyHad++;
                continue;
            }

            // get the previously-generated ContestPrediction
            var prediction = await _dataContext.ContestPredictions
                .AsNoTracking()
                .Where(x => x.ContestId == matchup.ContestId &&
                            x.PredictionType == pickemGroupPickType)
                .OrderByDescending(x => x.CreatedUtc)
                .FirstOrDefaultAsync(cancellationToken);

            // No ContestPrediction of the LEAGUE's pick type for this contest.
            // Counted and reported below: this was a bare `continue`, so a run
            // that produced nothing was indistinguishable from a run with
            // nothing to do — which is what made the empty 2026 sweep invisible.
            if (prediction is null)
            {
                noPrediction++;
                continue;
            }

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
                FranchiseSeasonId = finalPickFranchiseId,
                PickemGroupId = pickemGroupId,
                PickType = prediction.PredictionType == PickType.StraightUp ?
                    PickType.StraightUp : PickType.AgainstTheSpread,
                Week = seasonWeekNumber,
                TiebreakerType = TiebreakerType.TotalPoints
            };

            await _dataContext.UserPicks.AddAsync(synPick, cancellationToken);
            insertedPickIds.Add(synPick.Id);
            written.Add(matchup.ContestId);
            picksAdded++;
        }

        // Batch save all picks for this synthetic in this group
        if (picksAdded > 0)
        {
            await _dataContext.SaveChangesAsync(cancellationToken);
            await ReconcileConfidenceAsync(pickemGroupId, syntheticId, seasonWeekNumber, insertedPickIds, cancellationToken);
        }

        // Always reported, including the zero case. Every branch above is a
        // silent `continue`, so without this the only evidence of a no-op run
        // was the absence of a log line.
        _logger.Log(
            picksAdded > 0 ? LogLevel.Information : LogLevel.Warning,
            "Metric picks for {SyntheticId} in group {GroupId} week {Week}: " +
            "{Created} created, {AlreadyHad} already present, {NoPrediction} without a {PickType} prediction, of {Total} matchup(s). PickStyle={PickStyle}",
            syntheticId, pickemGroupId, seasonWeekNumber,
            picksAdded, alreadyHad, noPrediction,
            pickemGroupPickType, groupMatchups.Matchups.Count,
            syntheticPickStyle ?? "(none)");

        return written;
    }

    /// <summary>
    /// Assigns this synthetic's confidence points across one league-week: most
    /// points to the prediction it was most sure of.
    /// </summary>
    /// <remarks>
    /// Confidence leagues score a pick by its assigned confidence
    /// (PickScoringService: <c>ConfidencePoints ?? 0</c>), so a null means the
    /// bot scores ZERO on every correct pick — last place by construction,
    /// presented as a real standing.
    /// <para>
    /// Ranked by how far <c>WinProbability</c> sits from 0.5, which is the same
    /// number the threshold logic already reads: 0.9 and 0.1 are both confident
    /// calls, 0.55 is a coin flip. Deliberately identical across all four bots,
    /// so they differ only where a style actually flips a pick rather than in
    /// how the points are spread.
    /// </para>
    /// <para>
    /// Locked picks — already scored, or kicked off and not inserted by THIS
    /// call — keep their values, which are then reserved so the remaining
    /// 1..N stay distinct. Renumbering a scored pick would desync the points
    /// shown from the points awarded.
    /// </para>
    /// </remarks>
    private async Task ReconcileConfidenceAsync(
        Guid pickemGroupId,
        Guid syntheticId,
        int seasonWeekNumber,
        IReadOnlySet<Guid> newlyInsertedPickIds,
        CancellationToken ct)
    {
        var usesConfidence = await _dataContext.PickemGroups
            .AsNoTracking()
            .Where(g => g.Id == pickemGroupId)
            .Select(g => g.UseConfidencePoints)
            .FirstOrDefaultAsync(ct);

        if (!usesConfidence) return;

        var picks = await _dataContext.UserPicks
            .Where(p => p.UserId == syntheticId
                        && p.PickemGroupId == pickemGroupId
                        && p.Week == seasonWeekNumber)
            .ToListAsync(ct);

        if (picks.Count == 0) return;

        var contestIds = picks.Select(p => p.ContestId).Distinct().ToList();

        var kickoffByContest = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.GroupId == pickemGroupId && contestIds.Contains(m.ContestId))
            .Select(m => new { m.ContestId, m.StartDateUtc })
            .ToDictionaryAsync(x => x.ContestId, x => x.StartDateUtc, ct);

        var predictions = await _dataContext.ContestPredictions
            .AsNoTracking()
            .Where(x => contestIds.Contains(x.ContestId))
            .Select(x => new { x.ContestId, x.WinProbability, x.CreatedUtc })
            .ToListAsync(ct);

        var convictionByContest = predictions
            .GroupBy(x => x.ContestId)
            .ToDictionary(
                g => g.Key,
                g => Math.Abs((double)g.OrderByDescending(x => x.CreatedUtc).First().WinProbability - 0.5));

        var now = _dateTimeProvider.UtcNow();

        var locked = picks
            .Where(p => !newlyInsertedPickIds.Contains(p.Id)
                        && (p.ScoredAt != null
                            || (kickoffByContest.TryGetValue(p.ContestId, out var k) && k <= now)))
            .ToList();

        var reserved = locked
            .Where(p => p.ConfidencePoints.HasValue)
            .Select(p => p.ConfidencePoints!.Value)
            .ToHashSet();

        var mutable = picks.Except(locked).ToList();
        if (mutable.Count == 0) return;

        var available = Enumerable.Range(1, picks.Count)
            .Where(v => !reserved.Contains(v))
            .OrderByDescending(v => v)
            .ToList();

        // A contest with no prediction sorts last rather than dropping out:
        // every pick in a confidence league must carry a value.
        var ranked = mutable
            .OrderByDescending(p => convictionByContest.TryGetValue(p.ContestId, out var c) ? c : -1)
            .ThenBy(p => kickoffByContest.TryGetValue(p.ContestId, out var k) ? k : DateTime.MaxValue)
            .ThenBy(p => p.ContestId)
            .ToList();

        var changed = 0;
        for (var i = 0; i < ranked.Count && i < available.Count; i++)
        {
            if (ranked[i].ConfidencePoints == available[i]) continue;

            ranked[i].ConfidencePoints = available[i];
            ranked[i].ModifiedUtc = now;
            ranked[i].ModifiedBy = syntheticId;
            changed++;
        }

        if (changed > 0)
        {
            await _dataContext.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Confidence reconciled for {SyntheticId} in group {GroupId} week {Week}: {Picks} pick(s), {Locked} locked, {Changed} changed.",
                syntheticId, pickemGroupId, seasonWeekNumber, picks.Count, locked.Count, changed);
        }
    }

    private Guid DeterminePickWithThreshold(
        ContestPrediction prediction,
        LeagueWeekMatchupsDto.MatchupForPickDto matchup,
        string? pickStyle)
    {
        if (prediction == null)
            throw new ArgumentNullException(nameof(prediction));
        
        if (matchup == null)
            throw new ArgumentNullException(nameof(matchup));
        
        // A null/blank style is NOT an error: it means "no threshold", i.e.
        // take the model's prediction unmodified. MetricBot is exactly that,
        // and treating it as invalid is what kept it pickless.
        if (string.IsNullOrWhiteSpace(pickStyle))
            return prediction.WinnerFranchiseSeasonId;

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
