using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Scoring;
using SportsData.Api.Application.Scoring.Jobs.PickScoring;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.Picks.Commands.BackfillUserPickBetPoints;

public interface IApplyUserPickBetPoints
{
    Task Process(ApplyUserPickBetPointsCommand command);
}

/// <summary>
/// Hangfire job: computes the simulated bet columns for every scored pick on
/// ONE contest, through the same <see cref="IPickScoringService.ScoreSimulatedBets"/>
/// the scoring processor and the nightly audit use, from the contest's
/// finalized result and each league's matchup prices. IsCorrect and
/// PointsAwarded are never written.
///
/// NotFound (the contest is not finalized in that Producer) and an
/// unfinalized result are logged and skipped: retrying cannot help, and the
/// audit owns unscoring such picks. Any other failure THROWS so Hangfire
/// retries this one contest.
/// </summary>
public class ApplyUserPickBetPointsHandler : IApplyUserPickBetPoints
{
    private readonly AppDataContext _dataContext;
    private readonly IContestClientFactory _contestClientFactory;
    private readonly IPickScoringService _pickScoringService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ApplyUserPickBetPointsHandler> _logger;

    public ApplyUserPickBetPointsHandler(
        AppDataContext dataContext,
        IContestClientFactory contestClientFactory,
        IPickScoringService pickScoringService,
        IDateTimeProvider dateTimeProvider,
        ILogger<ApplyUserPickBetPointsHandler> logger)
    {
        _dataContext = dataContext;
        _contestClientFactory = contestClientFactory;
        _pickScoringService = pickScoringService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task Process(ApplyUserPickBetPointsCommand command)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId,
            ["ContestId"] = command.ContestId,
            ["Sport"] = command.Sport
        });

        var response = await _contestClientFactory
            .Resolve(command.Sport)
            .GetMatchupResult(command.ContestId);

        if (response is Failure<MatchupResult> { Status: ResultStatus.NotFound })
        {
            _logger.LogWarning("Bet points: no finalized result in the {Sport} Producer; picks left unchanged.", command.Sport);
            return;
        }

        if (response is not Success<MatchupResult> { Value: { } result })
        {
            var errors = response is Failure<MatchupResult> failure
                ? string.Join("; ", failure.Errors.Select(e => e.ErrorMessage))
                : "no result returned";
            throw new InvalidOperationException(
                $"Matchup result fetch failed for contest {command.ContestId} ({command.Sport}): {errors}");
        }

        if (result.FinalizedUtc is null)
        {
            _logger.LogWarning("Bet points: result is not finalized; picks left unchanged.");
            return;
        }

        var picks = await _dataContext.UserPicks
            .Include(p => p.Group)
            .Where(p => p.ContestId == command.ContestId
                     && p.ScoredAt != null
                     && p.Group.Sport == command.Sport)
            .ToListAsync();

        var pricingByGroup = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.ContestId == command.ContestId)
            .Select(m => new { m.GroupId, Pricing = new MatchupPricing(m.AwayMoneyLine, m.HomeMoneyLine, m.AwaySpreadPrice, m.HomeSpreadPrice) })
            .ToDictionaryAsync(m => m.GroupId, m => m.Pricing);

        var changed = 0;

        foreach (var pick in picks)
        {
            _pickScoringService.ScoreSimulatedBets(
                pick.Group,
                result.Spread,
                pick,
                result,
                pricingByGroup.GetValueOrDefault(pick.PickemGroupId));

            // Stamp only when EF detected a change: a rewrite must leave a
            // trace, a no-op (re-run) must not.
            if (_dataContext.Entry(pick).State == EntityState.Modified)
            {
                pick.ModifiedUtc = _dateTimeProvider.UtcNow();
                pick.ModifiedBy = Guid.Empty;
                changed++;
            }
        }

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Bet points applied: {Changed} of {Count} scored picks changed.", changed, picks.Count);
    }
}
