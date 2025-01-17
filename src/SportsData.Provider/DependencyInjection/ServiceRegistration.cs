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
            services.AddSingleton<FootballNcaaFranchisesDocumentJobDefinition>();
            services.AddSingleton<FootballNcaaVenuesDocumentJobDefinition>();

            var seasonsToProcess = new List<int>
            {
                2024
            };

            foreach (var season in seasonsToProcess)
            {
                //var def = new FootballNcaaTeamsBySeasonDocumentJobDefinition()
                //{
                //    SeasonYear = season
                //};
                //services.AddSingleton(def);

                var def2 = new FootballNcaaGroupsBySeasonDocumentJobDefinition()
                {
                    SeasonYear = season
                };
                services.AddSingleton(def2);

                //var def3 = new FootballNcaaAthletesBySeasonDocumentJobDefinition()
                //{
                //    SeasonYear = season
                //};
                //services.AddSingleton(def3);
            }

            //var jobDefinitions = new List<DocumentProviderJob<T>()
            //{
            //    new DocumentProviderJob<FootballNflVenueDocumentJobDefinition>()
            //};

            switch (mode)
            {
                case Sport.All:
                    // need all job definitions
                    //services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaAthletesBySeasonDocumentJobDefinition>>();
                    //services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaFranchisesDocumentJobDefinition>>();
                    services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaGroupsBySeasonDocumentJobDefinition>>();
                    //services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaTeamsBySeasonDocumentJobDefinition>>();
                    //services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaVenuesDocumentJobDefinition>>();
                    break;
                case Sport.Football:
                    // need all job definitions where Sport = Football
                    // how to do that?
                    services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNflVenueDocumentJobDefinition>>();
                    break;
                case Sport.FootballNcaa:
                    // need all job definitions where Sport = FootballNcaa
                    // how to do that?
                    services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaAthletesBySeasonDocumentJobDefinition>>();
                    services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaFranchisesDocumentJobDefinition>>();
                    services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaGroupsBySeasonDocumentJobDefinition>>();
                    services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaTeamsBySeasonDocumentJobDefinition>>();
                    services.AddScoped<IProvideDocuments, DocumentProviderJob<FootballNcaaVenuesDocumentJobDefinition>>();
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

            //BackgroundJob.Enqueue<DocumentProviderJob<FootballNcaaVenuesDocumentJobDefinition>>(job => job.ExecuteAsync());
            //BackgroundJob.Enqueue<DocumentProviderJob<FootballNcaaFranchisesDocumentJobDefinition>>(job => job.ExecuteAsync());
            BackgroundJob.Enqueue<DocumentProviderJob<FootballNcaaGroupsBySeasonDocumentJobDefinition>>(job => job.ExecuteAsync());
            //BackgroundJob.Enqueue<DocumentProviderJob<FootballNcaaTeamsBySeasonDocumentJobDefinition>>(job => job.ExecuteAsync());

            //BackgroundJob.Enqueue<DocumentProviderJob<EspnDocumentJobTeamSeasonDefinition>>(job => job.ExecuteAsync());

            return services;
        }
    }
}
