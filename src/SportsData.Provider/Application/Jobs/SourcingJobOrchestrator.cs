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
    public interface ISourcingJobOrchestrator
    {
        Task ExecuteAsync();
    }

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
                if (!resource.CronExpression.IsValidCron())
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
            // determine if any non-recurring jobs are currently processing
            var any = await _dbContext.ResourceIndexJobs
                .AnyAsync(x => x.SportId == _mode.CurrentSport &&
                               !x.IsRecurring &&
                               x.IsEnabled &&
                               x.IsQueued &&
                               x.LastCompletedUtc == null);

            if (any)
            {
                _logger.LogWarning("One-time ResourceIndex jobs are currently processing. Skipping refresh of one-time jobs.");
                return; // don't add any new jobs if there are already queued jobs
            }

            var nextJobToProcess = await _dbContext.ResourceIndexJobs
                .Where(x => x.SportId == _mode.CurrentSport &&
                            !x.IsRecurring &&
                            x.IsEnabled &&
                            !x.IsQueued &&
                            !x.LastCompletedUtc.HasValue)
                .OrderBy(x => x.Ordinal)
                .Take(1)
                .FirstOrDefaultAsync();

            if (nextJobToProcess is null)
            {
                _logger.LogInformation("No non-recurring jobs to schedule");
            }
            else
            {
                // do one more quick-check to ensure something else did not begin processing
                any = await _dbContext.ResourceIndexJobs
                    .AnyAsync(x => x.SportId == _mode.CurrentSport &&
                                   !x.IsRecurring &&
                                   x.IsEnabled &&
                                   x.IsQueued &&
                                   x.LastCompletedUtc == null);
                if (any)
                {
                    _logger.LogWarning("Second Check failed. One-time ResourceIndex jobs are currently processing. Skipping refresh of one-time jobs.");
                    return; // don't add any new jobs if there are already queued jobs
                }

                _logger.LogInformation("Scheduling one-time ResourceIndex job for {Name} (Id: {Id})", nextJobToProcess.Name, nextJobToProcess.Id);
                
                nextJobToProcess.IsQueued = true;
                await _dbContext.SaveChangesAsync();

                var def = new DocumentJobDefinition(nextJobToProcess);
                _backgroundJobProvider.Enqueue<IProcessResourceIndexes>(job => job.ExecuteAsync(def));
            }
        }
    }

}
