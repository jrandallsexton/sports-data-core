using Hangfire;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Documents;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Data.Entities;
using SportsData.Provider.Infrastructure.Providers.Espn;

namespace SportsData.Provider.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode)
        {
            /* Hangfire Jobs */
            switch (mode)
            {
                case Sport.All:
                    // need all job definitions
                    //services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaAthletesBySeasonDocumentJobDefinition>>();
                    //services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaFranchisesDocumentJobDefinition>>();
                    //services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaGroupsBySeasonDocumentProviderJobDefinition>>();
                    //services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaTeamsBySeasonDocumentJobDefinition>>();
                    //services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaVenuesDocumentJobDefinition>>();
                    break;
                case Sport.Football:
                    // need all job definitions where Sport = Football
                    // how to do that?
                    //services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNflVenueDocumentJobDefinition>>();
                    break;
                case Sport.FootballNcaa:
                    // need all job definitions where Sport = FootballNcaa
                    // how to do that?
                    //services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaAthletesBySeasonDocumentProviderJobDefinition>>();
                    //services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaFranchisesDocumentProviderJobDefinition>>();
                    //services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaGroupsBySeasonDocumentProviderJobDefinition>>();
                    //services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaTeamsBySeasonDocumentProviderJobDefinition>>();
                    break;
                case Sport.FootballNfl:
                    // need all job definitions where Sport = FootballNfl
                    // how to do that?
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            services.AddDataPersistenceExternal();
            services.AddScoped<IDecodeDocumentProvidersAndTypes, DocumentProviderAndTypeDecoder>();
            services.AddScoped<IProcessResourceIndexes, ResourceIndexJob>();
            services.AddScoped<IProcessResourceIndexItems, ResourceIndexItemProcessor>();
            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();
            services.AddScoped<IProvideEspnApiData, EspnApiClient>();
            services.AddSingleton(new EspnApiClientConfig());

            return services;
        }

        public static async Task<IServiceProvider> ConfigureHangfireJobs(this IServiceProvider services, Sport mode)
        {
            var serviceScope = services.CreateScope();
            var recurringJobManager = serviceScope.ServiceProvider.GetService<IRecurringJobManager>();
            var backgroundJobProvider = serviceScope.ServiceProvider.GetRequiredService<IProvideBackgroundJobs>();
            var appDataContext = serviceScope.ServiceProvider.GetService<AppDataContext>();

            List<ResourceIndex> resources = null;

            switch (mode)
            {
                case Sport.All:
                case Sport.Football:
                    resources = await appDataContext.Resources
                        .Where(x => (x.SportId == Sport.Football ||
                                    x.SportId == Sport.FootballNcaa ||
                                    x.SportId == Sport.FootballNfl) &&
                                    !x.IsRecurring && x.IsEnabled)
                        .ToListAsync();
                    break;
                case Sport.FootballNfl:
                case Sport.FootballNcaa:
                    resources = await appDataContext.Resources
                        .Where(x => x.SportId == mode &&
                                    !x.IsRecurring &&
                                    x.IsEnabled)
                        .ToListAsync();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            if (resources == null)
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
