using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Documents.Processors;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
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
            services.AddDataPersistenceExternal();
            services.AddScoped<AthleteDocumentProcessor>();
            services.AddScoped<AthleteImageRequestProcessor>();
            services.AddScoped<AthleteImageResponseProcessor>();
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
            services.AddScoped<PositionDocumentProcessor>();
            services.AddScoped<TeamDocumentProcessor>();
            services.AddScoped<TeamInformationDocumentProcessor>();
            services.AddScoped<TeamSeasonDocumentProcessor>();
            services.AddScoped<VenueDocumentProcessor>();
            services.AddScoped<VenueImageRequestProcessor>();
            services.AddScoped<VenueImageResponseProcessor>();
            return services;
        }

        public static IServiceProvider ConfigureHangfireJobs(this IServiceProvider services, Sport mode)
        {
            var serviceScope = services.CreateScope();

            var backgroundJobProvider = serviceScope.ServiceProvider.GetRequiredService<IProvideBackgroundJobs>();

            var documentTypesToLoad = new List<DocumentType>()
            {
                DocumentType.Venue,
                DocumentType.Franchise,
                DocumentType.Position,
                DocumentType.Athlete
            };

            foreach (var docType in documentTypesToLoad)
            {
                backgroundJobProvider.Enqueue<IProvideProviders>(x => x.PublishDocumentEvents(
                    new PublishDocumentEventsCommand()
                    {
                        SourceDataProvider = SourceDataProvider.Espn,
                        Sport = mode,
                        DocumentType = docType
                    }));
            }

            var documentTypesBySeasonToLoad = new List<DocumentType>()
            {
                DocumentType.AthleteBySeason,
                DocumentType.GroupBySeason,
                DocumentType.TeamBySeason,
                DocumentType.CoachBySeason
            };

            var seasonsToLoad = new List<int>()
            {
                //2020,
                //2021,
                //2022,
                //2023,
                2024
            };

            foreach (var docType in documentTypesBySeasonToLoad)
            {
                foreach (var season in seasonsToLoad)
                {
                    backgroundJobProvider.Enqueue<IProvideProviders>(x => x.PublishDocumentEvents(
                        new PublishDocumentEventsCommand()
                        {
                            SourceDataProvider = SourceDataProvider.Espn,
                            Sport = mode,
                            DocumentType = docType,
                            Season = season
                        }));
                }
            }

            return services;
        }
    }
}
