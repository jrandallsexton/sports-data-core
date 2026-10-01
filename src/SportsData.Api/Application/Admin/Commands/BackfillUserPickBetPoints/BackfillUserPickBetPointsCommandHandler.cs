using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Admin.Commands.BackfillUserPickBetPoints;

public interface IBackfillUserPickBetPointsCommandHandler
{
    Task<Result<BackfillUserPickBetPointsResult>> ExecuteAsync(
        BackfillUserPickBetPointsCommand command,
        CancellationToken cancellationToken);
}

/// <summary>
/// Enqueues one <see cref="IApplyUserPickBetPoints"/> job per distinct
/// (sport, contest) carrying at least one scored pick, all under one
/// correlation id, and returns immediately. Each job fetches that contest's
/// result once, so a Producer failure retries that contest alone. Re-running
/// is safe: the jobs recompute from the same inputs.
/// </summary>
public class BackfillUserPickBetPointsCommandHandler : IBackfillUserPickBetPointsCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly ILogger<BackfillUserPickBetPointsCommandHandler> _logger;

    public BackfillUserPickBetPointsCommandHandler(
        AppDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider,
        ILogger<BackfillUserPickBetPointsCommandHandler> logger)
    {
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
        _logger = logger;
    }

    public async Task<Result<BackfillUserPickBetPointsResult>> ExecuteAsync(
        BackfillUserPickBetPointsCommand command,
        CancellationToken cancellationToken)
    {
        // The league's sport decides which Producer owns the contest.
        var contests = await _dataContext.UserPicks
            .AsNoTracking()
            .Where(p => p.ScoredAt != null)
            .Join(_dataContext.PickemGroups, p => p.PickemGroupId, g => g.Id, (p, g) => new { g.Sport, p.ContestId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var correlationId = Guid.NewGuid();

        foreach (var contest in contests)
        {
            var job = new ApplyUserPickBetPointsCommand(contest.Sport, contest.ContestId, correlationId);
            _backgroundJobProvider.Enqueue<IApplyUserPickBetPoints>(p => p.Process(job));
        }

        var sports = contests
            .GroupBy(c => c.Sport)
            .OrderBy(g => g.Key)
            .Select(g => new BackfillUserPickBetPointsSportResult(g.Key, g.Count()))
            .ToList();

        _logger.LogInformation(
            "User pick bet points backfill enqueued {Count} contest jobs. CorrelationId={CorrelationId}, BySport={BySport}",
            contests.Count, correlationId, string.Join(", ", sports.Select(s => $"{s.Sport}={s.ContestsEnqueued}")));

        return new Success<BackfillUserPickBetPointsResult>(
            new BackfillUserPickBetPointsResult(correlationId, contests.Count, sports),
            ResultStatus.Accepted);
    }
}
