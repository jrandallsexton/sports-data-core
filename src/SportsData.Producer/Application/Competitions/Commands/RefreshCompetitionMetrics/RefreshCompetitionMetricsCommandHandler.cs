using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions.Commands.CalculateCompetitionMetrics;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMetrics;

public interface IRefreshCompetitionMetricsCommandHandler
{
    Task<Result<RefreshCompetitionMetricsResult>> ExecuteAsync(
        RefreshCompetitionMetricsCommand command,
        CancellationToken cancellationToken = default);
}

public class RefreshCompetitionMetricsCommandHandler : IRefreshCompetitionMetricsCommandHandler
{
    private const int ExpectedCompetitionMetricsCount = 2; // both teams should have metrics

    private readonly TeamSportDataContext _dataContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;

    public RefreshCompetitionMetricsCommandHandler(
        TeamSportDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider)
    {
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
    }

    public async Task<Result<RefreshCompetitionMetricsResult>> ExecuteAsync(
        RefreshCompetitionMetricsCommand command,
        CancellationToken cancellationToken = default)
    {
        var contests = await _dataContext.Contests
            .Include(x => x.Competitions)
            .ThenInclude(comp => comp.Metrics)
            .Where(c => c.FinalizedUtc != null && c.SeasonYear == command.SeasonYear)
            .OrderBy(c => c.StartDateUtc)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var enqueuedCount = 0;

        foreach (var contest in contests)
        {
            var competition = contest.Competitions.FirstOrDefault();

            if (competition is null)
                continue;

            if (competition.Metrics.Count == ExpectedCompetitionMetricsCount)
                continue;

            var calculateCommand = new CalculateCompetitionMetricsCommand(competition.Id);
            _backgroundJobProvider.Enqueue<ICalculateCompetitionMetricsCommandHandler>(h =>
                h.ExecuteAsync(calculateCommand, CancellationToken.None));

            enqueuedCount++;
        }

        var result = new RefreshCompetitionMetricsResult(
            command.SeasonYear,
            contests.Count,
            enqueuedCount,
            $"Enqueued {enqueuedCount} metric calculation jobs for {contests.Count} contests in season {command.SeasonYear}");

        return new Success<RefreshCompetitionMetricsResult>(result);
    }
}
