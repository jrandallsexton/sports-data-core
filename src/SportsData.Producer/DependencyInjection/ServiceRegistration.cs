using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Documents.Processors;
using SportsData.Producer.Application.Documents.Processors.Football.Ncaa.Espn;
using SportsData.Producer.Application.Images;
using SportsData.Producer.Application.Images.Processors.Requests;
using SportsData.Producer.Application.Images.Processors.Responses;

namespace SportsData.Producer.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode)
        {
            /* Local Services */
            //services.AddScoped<IProcessDocuments, GroupBySeasonDocumentProcessor>();
            services.AddDataPersistenceExternal();
            services.AddScoped<AthleteDocumentProcessor>();
            services.AddScoped<AwardDocumentProcessor>();
            services.AddScoped<ContestDocumentProcessor>();
            services.AddScoped<DocumentCreatedProcessor>();
            services.AddScoped<FranchiseDocumentProcessor>();
            services.AddScoped<FranchiseLogoRequestProcessor>();
            services.AddScoped<FranchiseLogoResponseProcessor>();
            services.AddScoped<FranchiseSeasonLogoRequestProcessor>();
            services.AddScoped<FranchiseSeasonLogoResponseProcessor>();
            services.AddScoped<GroupBySeasonDocumentProcessor>();
            services.AddScoped<GroupLogoRequestProcessor>();
            services.AddScoped<GroupLogoResponseProcessor>();
            services.AddScoped<GroupSeasonLogoRequestProcessor>();
            services.AddScoped<GroupSeasonLogoResponseProcessor>();
            services.AddScoped<IDocumentProcessorFactory, DocumentProcessorFactory>();
            services.AddScoped<IImageProcessorFactory, ImageProcessorFactory>();
            services.AddScoped<ImageProcessedProcessor>();
            services.AddScoped<ImageRequestedProcessor>();
            services.AddScoped<IProcessImageRequests, ImageRequestedProcessor>();
            services.AddScoped<IProcessProcessedImages, ImageProcessedProcessor>();
            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();
            services.AddScoped<TeamDocumentProcessor>();
            services.AddScoped<TeamInformationDocumentProcessor>();
            services.AddScoped<TeamSeasonDocumentProcessor>();
            services.AddScoped<VenueCreatedDocumentProcessor>();
            services.AddScoped<VenueImageRequestProcessor>();
            services.AddScoped<VenueImageResponseProcessor>();
            return services;
        }

        public static IServiceProvider ConfigureHangfireJobs(this IServiceProvider services, Sport mode)
        {
            var serviceScope = services.CreateScope();

            var backgroundJobProvider = serviceScope.ServiceProvider.GetRequiredService<IProvideBackgroundJobs>();

            //// ask for venues
            //backgroundJobProvider.Enqueue<IProvideProviders>(x => x.PublishDocumentEvents(
            //    new PublishDocumentEventsCommand()
            //    {
            //        SourceDataProvider = SourceDataProvider.Espn,
            //        Sport = Sport.FootballNcaa,
            //        DocumentType = DocumentType.Venue
            //    }));

            //// ask for franchises
            //backgroundJobProvider.Enqueue<IProvideProviders>(x => x.PublishDocumentEvents(
            //    new PublishDocumentEventsCommand()
            //    {
            //        SourceDataProvider = SourceDataProvider.Espn,
            //        Sport = Sport.FootballNcaa,
            //        DocumentType = DocumentType.Franchise
            //    }));

            // ask for GroupsBySeason (conferences)
            backgroundJobProvider.Enqueue<IProvideProviders>(x => x.PublishDocumentEvents(
                new PublishDocumentEventsCommand()
                {
                    SourceDataProvider = SourceDataProvider.Espn,
                    Sport = Sport.FootballNcaa,
                    DocumentType = DocumentType.GroupBySeason,
                    Season = 2024
                }));

            //// ask for TeamsBySeason (FranchiseSeasons)
            //backgroundJobProvider.Enqueue<IProvideProviders>(x => x.PublishDocumentEvents(
            //    new PublishDocumentEventsCommand()
            //    {
            //        SourceDataProvider = SourceDataProvider.Espn,
            //        Sport = Sport.FootballNcaa,
            //        DocumentType = DocumentType.TeamBySeason,
            //        Season = 2024
            //    }));

            // ask for AthletesBySeason (FranchiseSeasonAthletes)

            return services;
        }
    }
}
