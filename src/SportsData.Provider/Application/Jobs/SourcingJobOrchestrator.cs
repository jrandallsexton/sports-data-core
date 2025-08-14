using Hangfire;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.DependencyInjection;
using SportsData.Core.Extensions;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Data.Entities;

namespace SportsData.Provider.Application.Jobs
{
    public class SourcingJobOrchestrator : ISourcingJobOrchestrator
    {
        private readonly ILogger<SourcingJobOrchestrator> _logger;
        private readonly AppDataContext _dbContext;
        private readonly IRecurringJobManager _recurringJobManager;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IAppMode _mode;

        public SourcingJobOrchestrator(
            ILogger<SourcingJobOrchestrator> logger,
            AppDataContext dbContext,
            IRecurringJobManager recurringJobManager,
            IAppMode mode,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _recurringJobManager = recurringJobManager;
            _mode = mode;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync()
        {
            // 1) Handle one-off scheduled jobs (placeholder hooks)
            await ProcessScheduledJobs();

            // 2) Register recurring jobs
            await RefreshRecurringSourcingJobs();

            // 3) Kick a single one-time ResourceIndex if needed
            await RefreshOneTimeResourceIndexJobs();
        }

        private async Task ProcessScheduledJobs()
        {
            // (Intentionally left minimal; no ownership/claiming here)
            var singleExecutionJobs = await _dbContext.ScheduledJobs
                .Where(x => x.IsActive &&
                            x.ExecutionMode == SourcingExecutionMode.OneTime &&
                            !x.LastCompletedUtc.HasValue)
                .ToListAsync();

            foreach (var _ in singleExecutionJobs)
            {
                // TODO: wire scheduled jobs if/when you define them
            }

            var pollingJobs = await _dbContext.ScheduledJobs
                .Where(x => x.IsActive &&
                            x.ExecutionMode == SourcingExecutionMode.PollUntilConditionMet &&
                            !x.LastCompletedUtc.HasValue)
                .ToListAsync();

            foreach (var _ in pollingJobs)
            {
                // TODO: wire polling jobs if/when you define them
            }
        }

        private async Task RefreshRecurringSourcingJobs()
        {
            var allResources = await _dbContext.ResourceIndexJobs
                .Where(x => x.SportId == _mode.CurrentSport && x.IsRecurring)
                .OrderBy(x => x.Ordinal)
                .ToListAsync();

            // Register enabled
            foreach (var resource in allResources.Where(x => x.IsEnabled))
            {
                if (string.IsNullOrEmpty(resource.CronExpression) || !resource.CronExpression.IsValidCron())
                {
                    _logger.LogWarning("Skipping job registration: invalid cron '{Cron}' for ResourceIndex {Id} ({Name})",
                        resource.CronExpression, resource.Id, resource.Name);
                    continue;
                }

                // Use a stable, unique ID per resource
                var jobId = $"Resource:{resource.Id}";
                var def = new DocumentJobDefinition(resource);

                _recurringJobManager.AddOrUpdate<IProcessResourceIndexes>(
                    jobId,
                    job => job.ExecuteAsync(def),
                    resource.CronExpression);

                _logger.LogInformation("Registered recurring job {JobId} for {Name} with cron '{Cron}'",
                    jobId, resource.Name, resource.CronExpression);
            }

            // Remove disabled
            foreach (var resource in allResources.Where(x => !x.IsEnabled))
            {
                var jobId = $"Resource:{resource.Id}";
                _recurringJobManager.RemoveIfExists(jobId);
                _logger.LogInformation("Removed disabled recurring job {JobId} for {Name}", jobId, resource.Name);
            }
        }

        private async Task RefreshOneTimeResourceIndexJobs()
        {
            // If a non-recurring job is currently in-flight (worker will set IsQueued at claim time),
            // skip scheduling another. This is a soft check; ownership is enforced by the worker.
            var anyInFlight = await _dbContext.ResourceIndexJobs
                .AnyAsync(x => x.SportId == _mode.CurrentSport &&
                               !x.IsRecurring &&
                               x.IsEnabled &&
                               x.IsQueued &&
                               x.LastCompletedUtc == null);

            if (anyInFlight)
            {
                _logger.LogInformation("One-time ResourceIndex job is in-flight. Skipping scheduling.");
                return;
            }

            // Find the next eligible non-recurring job that isn't completed or currently queued.
            var next = await _dbContext.ResourceIndexJobs
                .Where(x => x.SportId == _mode.CurrentSport &&
                            !x.IsRecurring &&
                            x.IsEnabled &&
                            !x.IsQueued &&
                            !x.LastCompletedUtc.HasValue)
                .OrderBy(x => x.Ordinal)
                .FirstOrDefaultAsync();

            if (next is null)
            {
                _logger.LogInformation("No non-recurring ResourceIndex jobs to schedule.");
                return;
            }

            // Do NOT set ProcessingInstanceId here; the worker owns the claim.
            // We also avoid toggling IsQueued here—let the worker flip it atomically when it claims.
            _logger.LogInformation("Scheduling one-time ResourceIndex job for {Name} (Id: {Id})", next.Name, next.Id);

            var def = new DocumentJobDefinition(next);
            _backgroundJobProvider.Enqueue<IProcessResourceIndexes>(job => job.ExecuteAsync(def));
        }
    }
}
