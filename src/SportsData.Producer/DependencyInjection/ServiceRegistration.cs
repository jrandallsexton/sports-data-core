using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Documents.Processors;
using SportsData.Producer.Application.Documents.Processors.Football.Ncaa;
using SportsData.Producer.Application.Images;

namespace SportsData.Producer.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode)
        {
            /* Local Services */
            //services.AddScoped<IProcessDocuments, GroupBySeasonDocumentProcessor>();
            services.AddDataPersistenceExternal();
            services.AddScoped<DocumentCreatedProcessor>();
            services.AddScoped<AthleteDocumentProcessor>();
            services.AddScoped<AwardDocumentProcessor>();
            services.AddScoped<ContestDocumentProcessor>();
            services.AddScoped<FranchiseDocumentProcessor>();
            services.AddScoped<GroupBySeasonDocumentProcessor>();
            services.AddScoped<IDocumentProcessorFactory, DocumentProcessorFactory>();
            services.AddScoped<IProcessImageRequests, ImageRequestedProcessor>();
            services.AddScoped<IProcessProcessedImages, ImageProcessedProcessor>();
            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();
            services.AddScoped<TeamBySeasonDocumentProcessor>();
            services.AddScoped<TeamDocumentProcessor>();
            services.AddScoped<TeamInformationDocumentProcessor>();
            services.AddScoped<VenueCreatedDocumentProcessor>();
            return services;
        }

        public static async Task<IServiceProvider> ConfigureHangfireJobs(this IServiceProvider services, Sport mode)
        {
            var serviceScope = services.CreateScope();

            var backgroundJobProvider = serviceScope.ServiceProvider.GetRequiredService<IProvideBackgroundJobs>();

            // ask for venues
            backgroundJobProvider.Enqueue<IProvideProviders>(x => x.PublishDocumentEvents(
                new PublishDocumentEventsCommand()
                {
                    SourceDataProvider = SourceDataProvider.Espn,
                    Sport = Sport.FootballNcaa,
                    DocumentType = DocumentType.Venue
                }));

            // ask for franchises
            backgroundJobProvider.Enqueue<IProvideProviders>(x => x.PublishDocumentEvents(
                new PublishDocumentEventsCommand()
                {
                    SourceDataProvider = SourceDataProvider.Espn,
                    Sport = Sport.FootballNcaa,
                    DocumentType = DocumentType.Franchise
                }));

            // ask for GroupsBySeason (conferences)
            backgroundJobProvider.Enqueue<IProvideProviders>(x => x.PublishDocumentEvents(
                new PublishDocumentEventsCommand()
                {
                    SourceDataProvider = SourceDataProvider.Espn,
                    Sport = Sport.FootballNcaa,
                    DocumentType = DocumentType.GroupBySeason,
                    Season = 2024
                }));

            // ask for TeamsBySeason (FranchiseSeasons)
            backgroundJobProvider.Enqueue<IProvideProviders>(x => x.PublishDocumentEvents(
                new PublishDocumentEventsCommand()
                {
                    SourceDataProvider = SourceDataProvider.Espn,
                    Sport = Sport.FootballNcaa,
                    DocumentType = DocumentType.TeamBySeason,
                    Season = 2024
                }));

            // ask for AthletesBySeason (FranchiseSeasonAhteletes)

            return services;
        }
    }
}
