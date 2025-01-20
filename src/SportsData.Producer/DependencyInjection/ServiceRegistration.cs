using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
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
            services.AddDataPersistenceExternal();
            services.AddScoped<AthleteDocumentProcessor>();
            services.AddScoped<AwardDocumentProcessor>();
            services.AddScoped<ContestDocumentProcessor>();
            services.AddScoped<FranchiseDocumentProcessor>();
            services.AddScoped<GroupBySeasonDocumentProcessor>();
            //services.AddScoped<IProcessDocuments, GroupBySeasonDocumentProcessor>();
            services.AddScoped<IProcessImageRequests, ImageRequestedProcessor>();
            services.AddScoped<IProcessProcessedImages, ImageProcessedProcessor>();
            services.AddScoped<TeamDocumentProcessor>();
            services.AddScoped<TeamBySeasonDocumentProcessor>();
            services.AddScoped<TeamInformationDocumentProcessor>();
            services.AddScoped<VenueCreatedDocumentProcessor>();
            services.AddScoped<IDocumentProcessorFactory, DocumentProcessorFactory>();
            return services;
        }
    }
}
