using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Football.Ncaa;

namespace SportsData.Producer.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode)
        {
            /* Local Services */
            services.AddScoped<AthleteDocumentProcessor>();
            services.AddScoped<AwardDocumentProcessor>();
            services.AddScoped<ContestDocumentProcessor>();
            services.AddScoped<FranchiseDocumentProcessor>();
            services.AddScoped<TeamDocumentProcessor>();
            services.AddScoped<TeamBySeasonDocumentProcessor>();
            services.AddScoped<TeamInformationDocumentProcessor>();
            services.AddScoped<VenueCreatedDocumentProcessor>();
            services.AddSingleton<IDocumentProcessorFactory, DocumentProcessorFactory>();
            return services;
        }
    }
}
