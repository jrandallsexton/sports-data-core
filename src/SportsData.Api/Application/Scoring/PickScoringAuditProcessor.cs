using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Scoring;

public class PickScoringAuditProcessor : IPickScoringAudit
{
    private readonly ILogger<PickScoringAuditProcessor> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IContestClientFactory _contestClientFactory;
    private readonly IPickScoringService _pickScoringService;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PickScoringAuditProcessor(
        ILogger<PickScoringAuditProcessor> logger,
        AppDataContext dataContext,
        IContestClientFactory contestClientFactory,
        IPickScoringService pickScoringService,
        IProvideBackgroundJobs backgroundJobProvider,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _contestClientFactory = contestClientFactory;
        _pickScoringService = pickScoringService;
        _backgroundJobProvider = backgroundJobProvider;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task Process(AuditContestCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId,
                   ["ContestId"] = command.ContestId,
                   ["Sport"] = command.Sport
               }))
        {
            // Load scored picks (audit only re-checks already-scored picks;
            // unscored picks are PickScoringJob's territory).
            var scoredPicks = await _dataContext.UserPicks
                .Include(p => p.Group)
                .Where(p => p.ContestId == command.ContestId && p.ScoredAt != null)
                .ToListAsync();

            if (scoredPicks.Count == 0)
            {
                _logger.LogInformation(
                    "PickScoringAudit: no scored picks for contest. Skipping.");
                return;
            }

            var matchupResultResponse = await _contestClientFactory
                .Resolve(command.Sport)
                .GetMatchupResult(command.ContestId);

            var affectedLeagueWeeks = new HashSet<(Guid LeagueId, int SeasonYear, int Week)>();

            if (matchupResultResponse.Status == ResultStatus.NotFound)
            {
                await HandleUnfinalizedAsync(scoredPicks, affectedLeagueWeeks, command);
            }
            else if (!matchupResultResponse.IsSuccess)
            {
                _logger.LogError(
                    "PickScoringAudit: GetMatchupResult failed. Status={Status}. Next nightly run will retry.",
                    matchupResultResponse.Status);
                return;
            }
            else if (matchupResultResponse.Value.FinalizedUtc is null)
            {
                // SQL filter should prevent this; defensive log so a reverted
                // filter shows up in Seq instead of silently corrupting picks.
                _logger.LogWarning(
                    "PickScoringAudit: GetMatchupResult returned Success with null FinalizedUtc. SQL filter may be reverted. Skipping.");
                return;
            }
            else
            {
                await HandleMismatchCheckAsync(
                    scoredPicks,
                    matchupResultResponse.Value,
                    affectedLeagueWeeks,
                    command);
            }

            await _dataContext.SaveChangesAsync();

            FanOutLeagueWeekScoring(affectedLeagueWeeks, command.CorrelationId);
        }
    }

    private async Task HandleUnfinalizedAsync(
        List<PickemGroupUserPick> scoredPicks,
        HashSet<(Guid LeagueId, int SeasonYear, int Week)> affectedLeagueWeeks,
        AuditContestCommand command)
    {
        // Per-(GroupId) (SeasonYear, SeasonWeek) lookup for fan-out. The
        // matchup row carries season metadata; the pick alone doesn't.
        var matchupKeys = await _dataContext.PickemGroupMatchups
            .Where(m => m.ContestId == command.ContestId)
            .Select(m => new { m.GroupId, m.SeasonYear, m.SeasonWeek })
            .ToListAsync();

        var keyByGroup = matchupKeys.ToDictionary(
            m => m.GroupId,
            m => (m.SeasonYear, m.SeasonWeek));

        var now = _dateTimeProvider.UtcNow();

        foreach (var pick in scoredPicks)
        {
            _logger.LogError(
                "PickScoringAudit correction: PickId={PickId} StoredIsCorrect={StoredIsCorrect} ComputedIsCorrect=null StoredPoints={StoredPoints} ComputedPoints=null LeagueId={LeagueId} Mode=Unfinalized",
                pick.Id, pick.IsCorrect, pick.PointsAwarded, pick.PickemGroupId);

            pick.IsCorrect = null;
            pick.PointsAwarded = null;
            pick.ScoredAt = null;
            pick.WasAgainstSpread = null;
            pick.ModifiedUtc = now;
            pick.ModifiedBy = CausationId.Api.PickScoringAuditProcessor;

            if (keyByGroup.TryGetValue(pick.PickemGroupId, out var key))
            {
                affectedLeagueWeeks.Add((pick.PickemGroupId, key.SeasonYear, key.SeasonWeek));
            }
        }
    }

    private async Task HandleMismatchCheckAsync(
        List<PickemGroupUserPick> scoredPicks,
        Core.Dtos.Canonical.MatchupResult result,
        HashSet<(Guid LeagueId, int SeasonYear, int Week)> affectedLeagueWeeks,
        AuditContestCommand command)
    {
        // Per-(GroupId) matchup lookup. Audit needs SeasonYear/Week per
        // affected league-week for fan-out, just like PickScoringProcessor.
        var matchupKeys = await _dataContext.PickemGroupMatchups
            .Where(m => m.ContestId == command.ContestId)
            .Select(m => new { m.GroupId, m.SeasonYear, m.SeasonWeek })
            .ToListAsync();

        var keyByGroup = matchupKeys.ToDictionary(
            m => m.GroupId,
            m => (m.SeasonYear, m.SeasonWeek));

        var now = _dateTimeProvider.UtcNow();

        foreach (var pick in scoredPicks)
        {
            if (pick.Group is null)
            {
                _logger.LogError(
                    "PickScoringAudit: Group navigation was null. PickId={PickId} LeagueId={LeagueId}. Skipping correction.",
                    pick.Id, pick.PickemGroupId);
                continue;
            }

            // Working copy: clone the inputs ScorePick reads, leave the
            // outputs ScorePick writes uninitialized so we can read them
            // post-call as the "computed" snapshot.
            var clone = new PickemGroupUserPick
            {
                Id = pick.Id,
                FranchiseId = pick.FranchiseId,
                ConfidencePoints = pick.ConfidencePoints
            };

            try
            {
                _pickScoringService.ScorePick(pick.Group, result.Spread, clone, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "PickScoringAudit: re-score threw. PickId={PickId} LeagueId={LeagueId}. Skipping correction.",
                    pick.Id, pick.PickemGroupId);
                continue;
            }

            var matches =
                pick.IsCorrect == clone.IsCorrect
                && pick.PointsAwarded == clone.PointsAwarded
                && pick.WasAgainstSpread == clone.WasAgainstSpread;

            if (matches)
            {
                continue;
            }

            var leagueKey = keyByGroup.TryGetValue(pick.PickemGroupId, out var key)
                ? key
                : ((int?)null, (int?)null);

            _logger.LogError(
                "PickScoringAudit correction: PickId={PickId} StoredIsCorrect={StoredIsCorrect} ComputedIsCorrect={ComputedIsCorrect} StoredPoints={StoredPoints} ComputedPoints={ComputedPoints} StoredWasAts={StoredWasAts} ComputedWasAts={ComputedWasAts} LeagueId={LeagueId} SeasonYear={SeasonYear} Week={Week} Mode=Mismatch",
                pick.Id,
                pick.IsCorrect, clone.IsCorrect,
                pick.PointsAwarded, clone.PointsAwarded,
                pick.WasAgainstSpread, clone.WasAgainstSpread,
                pick.PickemGroupId, leagueKey.Item1, leagueKey.Item2);

            pick.IsCorrect = clone.IsCorrect;
            pick.PointsAwarded = clone.PointsAwarded;
            pick.WasAgainstSpread = clone.WasAgainstSpread;
            // Preserve original ScoredAt — it's the audit trail of when
            // scoring first ran. ModifiedUtc records the correction time.
            pick.ModifiedUtc = now;
            pick.ModifiedBy = CausationId.Api.PickScoringAuditProcessor;

            if (leagueKey.Item1.HasValue && leagueKey.Item2.HasValue)
            {
                affectedLeagueWeeks.Add((pick.PickemGroupId, leagueKey.Item1.Value, leagueKey.Item2.Value));
            }
        }
    }

    private void FanOutLeagueWeekScoring(
        HashSet<(Guid LeagueId, int SeasonYear, int Week)> affectedLeagueWeeks,
        Guid correlationId)
    {
        foreach (var (leagueId, seasonYear, week) in affectedLeagueWeeks)
        {
            try
            {
                _backgroundJobProvider.Enqueue<IScoreLeagueWeeks>(
                    p => p.Process(leagueId, seasonYear, week, correlationId));

                _logger.LogInformation(
                    "PickScoringAudit: enqueued league-week rescore. LeagueId={LeagueId} SeasonYear={SeasonYear} Week={Week}",
                    leagueId, seasonYear, week);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "PickScoringAudit: failed to enqueue league-week rescore. LeagueId={LeagueId} SeasonYear={SeasonYear} Week={Week}. LeagueWeekScoringJob nightly backstop will catch this.",
                    leagueId, seasonYear, week);
            }
        }
    }
}
