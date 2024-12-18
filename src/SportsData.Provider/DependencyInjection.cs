using Hangfire;

namespace SportsData.Provider
{
    public static class DependencyInjection
    {

        public static IServiceCollection ConfigureHangfire(this IServiceCollection services, IConfiguration config)
        {
            services.AddHangfire(x => x.UseSqlServerStorage(config.GetConnectionString("Hangfire")));
            services.AddHangfireServer(serverOptions =>
            {
                // https://codeopinion.com/scaling-hangfire-process-more-jobs-concurrently/
                serverOptions.WorkerCount = 50; // Environment.ProcessorCount * 3;
            });
            return services;
        }

        public static IServiceProvider ConfigureHangfireJobs(this IServiceProvider services)
        {
            var serviceScope = services.CreateScope();
            var recurringJobManager = serviceScope.ServiceProvider.GetService<IRecurringJobManager>();

            // TODO: Move job enablement to appSettings (cron values also)
            //var enableRecordingMover = false;

            //if (enableRecordingMover)
            //{
            //    recurringJobManager.AddOrUpdate<IMoveRecordings>(nameof(RecordingMoverJob),
            //        job => job.ExecuteAsync(), "15 * * * *");
            //}
            //else
            //{
            //    recurringJobManager.RemoveIfExists(nameof(RecordingMoverJob));
            //}

            //recurringJobManager.RemoveIfExists(nameof(RecordingImportJob));
            //recurringJobManager.RemoveIfExists(nameof(AutoRecordingJob));

            ////recurringJobManager.AddOrUpdate<IImportRecordings>(nameof(RecordingImportJob),
            ////    job => job.ExecuteAsync(), "59 * * * *");

            //recurringJobManager.AddOrUpdate<IAutoRecordChannels>(nameof(AutoRecordingJob),
            //    job => job.ExecuteAsync(), "5 * * * *");

            //recurringJobManager.AddOrUpdate<IProcessRecordingRequests>(nameof(RecordingRequestProcessorJob),
            //    job => job.ExecuteAsync(), "* * * * *");

            return services;
        }
    }
}