using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Admin.Commands.BackfillMatchupOddsPricing;

public interface IBackfillMatchupOddsPricingCommandHandler
{
    Task<Result<BackfillMatchupOddsPricingResult>> ExecuteAsync(
        BackfillMatchupOddsPricingCommand command,
        CancellationToken cancellationToken);
}

/// <summary>
/// Enqueues one <see cref="IApplyMatchupOddsPricing"/> job per distinct
/// (sport, contest) across every PickemGroupMatchup, all under one correlation
/// id, and returns immediately. Each job prices a single contest, so a
/// Producer failure retries that contest alone. Intended as a one-time run;
/// re-running is safe (the jobs are idempotent).
/// </summary>
public class BackfillMatchupOddsPricingCommandHandler : IBackfillMatchupOddsPricingCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly ILogger<BackfillMatchupOddsPricingCommandHandler> _logger;

    public BackfillMatchupOddsPricingCommandHandler(
        AppDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider,
        ILogger<BackfillMatchupOddsPricingCommandHandler> logger)
    {
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
        _logger = logger;
    }

    public async Task<Result<BackfillMatchupOddsPricingResult>> ExecuteAsync(
        BackfillMatchupOddsPricingCommand command,
        CancellationToken cancellationToken)
    {
        // Distinct contests per sport: the league's sport decides which
        // Producer owns the contest. A contest carried by several leagues is
        // priced once; its job updates every matchup row.
        var contests = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Join(_dataContext.PickemGroups, m => m.GroupId, g => g.Id, (m, g) => new { g.Sport, m.ContestId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var correlationId = Guid.NewGuid();

        foreach (var contest in contests)
        {
            var job = new ApplyMatchupOddsPricingCommand(contest.Sport, contest.ContestId, correlationId);
            _backgroundJobProvider.Enqueue<IApplyMatchupOddsPricing>(p => p.Process(job));
        }

        var sports = contests
            .GroupBy(c => c.Sport)
            .OrderBy(g => g.Key)
            .Select(g => new BackfillMatchupOddsPricingSportResult(g.Key, g.Count()))
            .ToList();

        _logger.LogInformation(
            "Odds pricing backfill enqueued {Count} contest jobs. CorrelationId={CorrelationId}, BySport={BySport}",
            contests.Count, correlationId, string.Join(", ", sports.Select(s => $"{s.Sport}={s.ContestsEnqueued}")));

        return new Success<BackfillMatchupOddsPricingResult>(
            new BackfillMatchupOddsPricingResult(correlationId, contests.Count, sports),
            ResultStatus.Accepted);
    }
}
