using Hangfire;
using SportsData.Provider.Application.Jobs;

namespace SportsData.Provider.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceProvider ConfigureHangfireJobs(this IServiceProvider services)
        {
            var serviceScope = services.CreateScope();
            var recurringJobManager = serviceScope.ServiceProvider.GetService<IRecurringJobManager>();

            recurringJobManager.RemoveIfExists(nameof(VenueProviderJob));

            //recurringJobManager.AddOrUpdate<IProvideVenues>(nameof(VenueProviderJob),
            //    job => job.ExecuteAsync(), "15 * * * *");

            return services;
        }
    }
}
