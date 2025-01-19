using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Blobs;
using SportsData.Producer.Application.Documents.Processors;
using SportsData.Producer.Application.Documents.Processors.Football.Ncaa;

namespace SportsData.Producer.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode)
        {
            /* Local Services */
            services.AddSingleton<IProvideBlobStorage, IProvideBlobStorage>();
            services.AddScoped<AthleteDocumentProcessor>();
            services.AddScoped<AwardDocumentProcessor>();
            services.AddScoped<ContestDocumentProcessor>();
            services.AddScoped<FranchiseDocumentProcessor>();
            services.AddScoped<GroupBySeasonDocumentProcessor>();
            services.AddScoped<IProcessDocuments, GroupBySeasonDocumentProcessor>();
            services.AddScoped<TeamDocumentProcessor>();
            services.AddScoped<TeamBySeasonDocumentProcessor>();
            services.AddScoped<TeamInformationDocumentProcessor>();
            services.AddScoped<VenueCreatedDocumentProcessor>();
            services.AddScoped<IDocumentProcessorFactory, DocumentProcessorFactory>();
            return services;
        }
    }
}
