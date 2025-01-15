using Hangfire;

using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Application.Jobs.Definitions;

namespace SportsData.Provider.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceProvider ConfigureHangfireJobs(this IServiceProvider services)
        {
            var serviceScope = services.CreateScope();
            var recurringJobManager = serviceScope.ServiceProvider.GetService<IRecurringJobManager>();

            //recurringJobManager.AddOrUpdate<IProvideDocuments>(
            //    nameof(DocumentProviderJob<EspnDocumentJobFranchiseDefinition>),
            //    job => job.ExecuteAsync(), "15 * * * *");

            //recurringJobManager.AddOrUpdate<IProvideDocuments>(
            //    nameof(DocumentProviderJob<EspnDocumentJobVenueDefinition>),
            //    job => job.ExecuteAsync(), "15 * * * *");

            BackgroundJob.Enqueue<DocumentProviderJob<EspnDocumentJobTeamSeasonDefinition>>(job => job.ExecuteAsync());

            return services;
        }
    }
}
