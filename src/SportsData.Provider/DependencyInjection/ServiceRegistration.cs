using Hangfire;

using SportsData.Core.Common;
using SportsData.Core.Common.Parsing;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Providers.Espn;

namespace SportsData.Provider.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode, bool useMongo)
        {
            services.AddDataPersistenceExternal();
            services.AddScoped<IProcessResourceIndexes, ResourceIndexJob>();
            services.AddScoped<IProcessResourceIndexItems, ResourceIndexItemProcessor>();
            services.AddScoped<IResourceIndexItemParser, ResourceIndexItemParser>();
            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();
            services.AddScoped<IProvideEspnApiData, EspnApiClient>();
            services.AddScoped<IProcessPublishDocumentEvents, PublishDocumentEventsProcessor>();

            if (useMongo)
            {
                services.AddSingleton<IDocumentStore, DocumentService>();
            }
            else
            {
                services.AddSingleton<IDocumentStore, CosmosDocumentService>();
            }

            services.AddSingleton(new EspnApiClientConfig());

            return services;
        }

        public static async Task<IServiceProvider> ConfigureHangfireJobs(
            this IServiceProvider services,
            Sport mode)
        {
            var serviceScope = services.CreateScope();
            var recurringJobManager = serviceScope.ServiceProvider.GetService<IRecurringJobManager>();

            recurringJobManager.AddOrUpdate<SourcingJobOrchestrator>(nameof(SourcingJobOrchestrator), job => job.ExecuteAsync(), Cron.Minutely);

            return services;
        }
    }
}
