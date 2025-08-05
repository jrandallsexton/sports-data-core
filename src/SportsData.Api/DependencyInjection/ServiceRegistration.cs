using SportsData.Api.Application.UI.Leagues.LeagueCreationPage;
using SportsData.Api.Application.UI.TeamCard;
using SportsData.Api.Application.UI.TeamCard.Handlers;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

namespace SportsData.Api.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode)
        {
            services.AddScoped<ITeamCardService, TeamCardService>();
            services.AddScoped<IGetTeamCardQueryHandler, GetTeamCardQueryHandler>();
            services.AddScoped<IProvideCanonicalData, CanonicalDataProvider>();
            services.AddScoped<ILeagueCreationService, LeagueCreationService>();
            services.AddScoped<ICreateLeagueCommandHandler, CreateLeagueCommandHandler>();
            return services;
        }
    }
}
