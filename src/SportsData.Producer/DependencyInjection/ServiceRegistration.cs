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
using SportsData.Producer.Application.Slugs;
using SportsData.Producer.Application.Venues;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode)
        {
            /* Local Services */
            services.AddScoped<IDataContextFactory, DataContextFactory>();

            services.AddDataPersistenceExternal();
            services.AddScoped<AthleteDocumentProcessor>();
            services.AddScoped<AthleteImageRequestProcessor<FootballDataContext>>();
            services.AddScoped<AthleteImageResponseProcessor<FootballDataContext>>();
            services.AddScoped<AwardDocumentProcessor>();
            services.AddScoped<ContestDocumentProcessor>();
            services.AddScoped<DocumentCreatedProcessor>();
            services.AddScoped<FranchiseDocumentProcessor<FootballDataContext>>();
            services.AddScoped<FranchiseLogoRequestProcessor<FootballDataContext>>();
            services.AddScoped<FranchiseLogoResponseProcessor<FootballDataContext>>();
            services.AddScoped<FranchiseSeasonLogoRequestProcessor<FootballDataContext>>();
            services.AddScoped<FranchiseSeasonLogoResponseProcessor<FootballDataContext>>();
            services.AddScoped<GroupBySeasonDocumentProcessor>();
            services.AddScoped<GroupLogoRequestProcessor<FootballDataContext>>();
            services.AddScoped<GroupLogoResponseProcessor<FootballDataContext>>();
            services.AddScoped<GroupSeasonLogoRequestProcessor<FootballDataContext>>();
            services.AddScoped<GroupSeasonLogoResponseProcessor<FootballDataContext>>();
            services.AddScoped<IDocumentProcessorFactory, DocumentProcessorFactory>();
            services.AddScoped<IImageProcessorFactory, ImageProcessorFactory>();
            services.AddScoped<ImageProcessedProcessor>();
            services.AddScoped<ImageRequestedProcessor>();
            services.AddScoped<IProcessImageRequests, ImageRequestedProcessor>();
            services.AddScoped<IProcessProcessedImages, ImageProcessedProcessor>();
            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();
            services.AddScoped<ISlugGenerator, DefaultSlugGenerator>();
            services.AddScoped<PositionDocumentProcessor<FootballDataContext>>();
            services.AddScoped<TeamDocumentProcessor>();
            services.AddScoped<TeamInformationDocumentProcessor>();
            services.AddScoped<TeamSeasonDocumentProcessor<FootballDataContext>>();
            services.AddScoped<VenueDocumentProcessor<FootballDataContext>>();
            services.AddScoped<VenueImageRequestProcessor<FootballDataContext>>();
            services.AddScoped<VenueImageResponseProcessor<FootballDataContext>>();

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
                //DocumentType.Position,
                //DocumentType.Athlete
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
                //DocumentType.AthleteBySeason,
                //DocumentType.GroupBySeason,
                //DocumentType.TeamBySeason,
                //DocumentType.CoachBySeason
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
