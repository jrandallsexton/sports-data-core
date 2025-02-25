using Hangfire;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Parsing;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Providers.Espn;

namespace SportsData.Provider.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode)
        {
            services.AddDataPersistenceExternal();
            services.AddScoped<IProcessResourceIndexes, ResourceIndexJob>();
            services.AddScoped<IProcessResourceIndexItems, ResourceIndexItemProcessor>();
            services.AddScoped<IResourceIndexItemParser, ResourceIndexItemParser>();
            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();
            services.AddScoped<IProvideEspnApiData, EspnApiClient>();
            services.AddSingleton(new EspnApiClientConfig());

            return services;
        }

        public static async Task<IServiceProvider> ConfigureHangfireJobs(
            this IServiceProvider services,
            Sport mode)
        {
            var serviceScope = services.CreateScope();
            var recurringJobManager = serviceScope.ServiceProvider.GetService<IRecurringJobManager>();
            var backgroundJobProvider = serviceScope.ServiceProvider.GetRequiredService<IProvideBackgroundJobs>();
            var appDataContext = serviceScope.ServiceProvider.GetRequiredService<AppDataContext>();

            var resources = await appDataContext.Resources
                .Where(x => x.SportId == mode &&
                            !x.IsRecurring &&
                            x.IsEnabled &&
                            (x.LastAccessed == null || x.LastAccessed < DateTime.UtcNow.AddHours(0))) // TODO: via config. for now, not in the last hour
                .OrderBy(x => x.Ordinal)
                .ToListAsync();

            if (resources is null)
            {
                throw new Exception("Found 0 resource indexes to process");
            }

            foreach (var resource in resources)
            {
                var def = new DocumentJobDefinition(resource);
                backgroundJobProvider.Enqueue<IProcessResourceIndexes>(job => job.ExecuteAsync(def));
            }

            //recurringJobManager.AddOrUpdate<IProvideDocuments>(
            //    nameof(DocumentProviderJob<EspnDocumentJobFranchiseDefinition>),
            //    job => job.ExecuteAsync(), "15 * * * *");

            //recurringJobManager.AddOrUpdate<IProvideDocuments>(
            //    nameof(DocumentProviderJob<EspnDocumentJobVenueDefinition>),
            //    job => job.ExecuteAsync(), "15 * * * *");

            return services;
        }
    }
}
