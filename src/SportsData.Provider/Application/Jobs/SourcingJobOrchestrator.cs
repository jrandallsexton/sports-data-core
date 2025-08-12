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
            // 1. Handle one-off scheduled jobs
            await ProcessScheduledJobs();

            // 2. Refresh recurring jobs based on ResourceIndex
            await RefreshRecurringSourcingJobs();

            // 3. Add one-time jobs for any ResourceIndexes that are non-recurring and need to be processed
            await RefreshOneTimeResourceIndexJobs();
        }

        private async Task ProcessScheduledJobs()
        {
            var singleExecutionJobs = await _dbContext.ScheduledJobs
                .Where(x => x.IsActive &&
                            x.ExecutionMode == SourcingExecutionMode.OneTime &&
                            !x.LastCompletedUtc.HasValue)
                .ToListAsync();

            foreach (var task in singleExecutionJobs)
            {
                // do what?
                //var def = new DocumentJobDefinition(resource);
                //backgroundJobProvider.Enqueue<IProcessResourceIndexes>(job => job.ExecuteAsync(def));
            }

            var pollingJobs = await _dbContext.ScheduledJobs
                .Where(x => x.IsActive &&
                            x.ExecutionMode == SourcingExecutionMode.PollUntilConditionMet &&
                            !x.LastCompletedUtc.HasValue)
                .ToListAsync();

            foreach (var task in pollingJobs)
            {
                // do what?
            }
        }

        private async Task RefreshRecurringSourcingJobs()
        {
            var allResources = await _dbContext.ResourceIndexJobs
                .Where(x => x.SportId == _mode.CurrentSport && x.IsRecurring)
                .OrderBy(x => x.Ordinal)
                .ToListAsync();

            foreach (var resource in allResources.Where(x => x.IsEnabled))
            {
                if (string.IsNullOrEmpty(resource.CronExpression) || !resource.CronExpression.IsValidCron())
                {
                    _logger.LogWarning("Skipping job registration: invalid cron '{Cron}' for ResourceIndex {Id} ({Name})",
                        resource.CronExpression, resource.Id, resource.Name);
                    continue;
                }

                var jobId = $"Resource_{resource.Name}";
                var def = new DocumentJobDefinition(resource);

                _recurringJobManager.AddOrUpdate<IProcessResourceIndexes>(
                    jobId,
                    job => job.ExecuteAsync(def),
                    resource.CronExpression);

                _logger.LogInformation("Registered recurring job {JobId} for {Name} with cron '{Cron}'",
                    jobId, resource.Name, resource.CronExpression);
            }

            foreach (var resource in allResources.Where(x => !x.IsEnabled))
            {
                var jobId = $"Resource_{resource.Name}";
                _recurringJobManager.RemoveIfExists(jobId);
                _logger.LogInformation("Removed disabled recurring job {JobId} for {Name}", jobId, resource.Name);
            }
        }

        private async Task RefreshOneTimeResourceIndexJobs()
        {
            // Pick the next eligible row
            var next = await _dbContext.ResourceIndexJobs
                .Where(x => x.SportId == _mode.CurrentSport &&
                            !x.IsRecurring &&
                            x.IsEnabled &&
                            !x.LastCompletedUtc.HasValue)
                .OrderBy(x => x.Ordinal)
                .Select(x => new { x.Id })
                .FirstOrDefaultAsync();

            if (next is null)
            {
                _logger.LogInformation("No non-recurring jobs to schedule");
                return;
            }

            var me = Guid.NewGuid();
            var now = DateTime.UtcNow;

            // Atomically claim (only if not already owned)
            var claimed = await _dbContext.ResourceIndexJobs
                .Where(x => x.Id == next.Id && x.ProcessingInstanceId == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ProcessingInstanceId, _ => me)
                    .SetProperty(x => x.ProcessingStartedUtc, _ => now)
                    .SetProperty(x => x.IsQueued, _ => true)) == 1;

            if (!claimed)
            {
                _logger.LogInformation("Skipped scheduling ResourceIndex {Id}: already owned/queued.", next.Id);
                return;
            }

            // Enqueue the worker; it will own execution and release claim in finally
            var resource = await _dbContext.ResourceIndexJobs.FindAsync(next.Id);
            if (resource is null)
            {
                _logger.LogWarning("Claimed ResourceIndex {Id} not found; releasing claim.", next.Id);
                await _dbContext.ResourceIndexJobs
                    .Where(x => x.Id == next.Id && x.ProcessingInstanceId == me)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.ProcessingInstanceId, _ => (Guid?)null)
                        .SetProperty(x => x.ProcessingStartedUtc, _ => (DateTime?)null)
                        .SetProperty(x => x.IsQueued, _ => false));
                return;
            }

            _logger.LogInformation("Scheduling one-time ResourceIndex job for {Name} (Id: {Id})", resource.Name, resource.Id);

            var def = new DocumentJobDefinition(resource);
            _backgroundJobProvider.Enqueue<IProcessResourceIndexes>(job => job.ExecuteAsync(def));
        }
    }
}
