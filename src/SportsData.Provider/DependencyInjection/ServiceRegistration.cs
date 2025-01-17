using Hangfire;

using SportsData.Core.Common;
using SportsData.Provider.Application.Documents;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Application.Jobs.Definitions.Espn.Football.Ncaa;
using SportsData.Provider.Application.Jobs.Definitions.Espn.Football.Nfl;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Infrastructure.Providers.Espn;

namespace SportsData.Provider.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode)
        {
            /* Hangfire Jobs */
            services.AddSingleton<FootballNcaaFranchiseDocumentJobDefinition>();
            services.AddSingleton<FootballNcaaVenueDocumentJobDefinition>();

            var def = new FootballNcaaTeamSeasonDocumentJobDefinition()
            {
                SeasonYear = 2024
            };
            services.AddSingleton(def);

            //var jobDefinitions = new List<DocumentProviderJob<T>()
            //{
            //    new DocumentProviderJob<FootballNflVenueDocumentJobDefinition>()
            //};

            switch (mode)
            {
                case Sport.All:
                    // need all job definitions
                    break;
                case Sport.Football:
                    // need all job definitions where Sport = Football
                    // how to do that?
                    services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNflVenueDocumentJobDefinition>>();
                    break;
                case Sport.FootballNcaa:
                    // need all job definitions where Sport = FootballNcaa
                    // how to do that?
                    services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaFranchiseDocumentJobDefinition>>();
                    services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaVenueDocumentJobDefinition>>();
                    break;
                case Sport.FootballNfl:
                    // need all job definitions where Sport = FootballNfl
                    // how to do that?
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            services.AddScoped<IProvideEspnApiData, EspnApiClient>();
            services.AddScoped<IProcessResourceIndexes, ResourceIndexItemProcessor>();
            services.AddScoped<IDecodeDocumentProvidersAndTypes, DocumentProviderAndTypeDecoder>();
            services.AddSingleton(new EspnApiClientConfig());

            return services;
        }

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

            //BackgroundJob.Enqueue<DocumentProviderJob<EspnDocumentJobVenueDefinition>>(job => job.ExecuteAsync());

            //BackgroundJob.Enqueue<DocumentProviderJob<EspnDocumentJobTeamSeasonDefinition>>(job => job.ExecuteAsync());

            return services;
        }
    }
}
