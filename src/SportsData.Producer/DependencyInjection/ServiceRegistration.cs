using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Documents.Processors;
using SportsData.Producer.Application.Images;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode)
        {
            services.AddScoped<IDataContextFactory, DataContextFactory>();

            services.AddDataPersistenceExternal();

            services.AddScoped<DocumentCreatedProcessor>();

            services.AddSingleton<IDocumentProcessorRegistry, DocumentProcessorRegistry>();
            services.Scan(scan => scan
                .FromAssemblyOf<IProcessDocuments>()
                .AddClasses(c => c.AssignableTo<IProcessDocuments>())
                .AsSelfWithInterfaces()
                .WithScopedLifetime());

            services.AddScoped<IDocumentProcessorFactory>(provider =>
            {
                var context = provider.GetRequiredService<BaseDataContext>();
                var logger = provider.GetRequiredService<ILogger<DocumentProcessorFactory>>();
                var registry = provider.GetRequiredService<IDocumentProcessorRegistry>();
                var factory = new DocumentProcessorFactory(provider, logger, context, registry);
                return factory;
            });

            services.AddScoped<IImageProcessorFactory>(provider =>
            {
                var appMode = provider.GetRequiredService<IAppMode>();
                var context = provider.GetRequiredService<BaseDataContext>();
                var logger = provider.GetRequiredService<ILogger<ImageProcessorFactory>>();
                var decoder = provider.GetRequiredService<IDecodeDocumentProvidersAndTypes>();

                return new ImageProcessorFactory(appMode, decoder, provider, context, logger);
            });

            services.AddScoped<ImageRequestedProcessor>();
            services.AddScoped<ImageProcessedProcessor>();

            services.AddScoped<IProcessImageRequests, ImageRequestedProcessor>();
            services.AddScoped<IProcessProcessedImages, ImageProcessedProcessor>();

            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();

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
                //DocumentType.AthleteSeason,
                //DocumentType.GroupSeason,
                //DocumentType.TeamSeason,
                //DocumentType.CoachSeason
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
