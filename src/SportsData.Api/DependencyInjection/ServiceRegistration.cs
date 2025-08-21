using Hangfire;

using SportsData.Api.Application.Jobs;
using SportsData.Api.Application.Processors;
using SportsData.Api.Application.UI.Leagues;
using SportsData.Api.Application.UI.Leagues.JoinLeague;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage;
using SportsData.Api.Application.UI.Messageboard;
using SportsData.Api.Application.UI.Picks;
using SportsData.Api.Application.UI.Picks.PicksPage;
using SportsData.Api.Application.UI.TeamCard;
using SportsData.Api.Application.UI.TeamCard.Handlers;
using SportsData.Api.Application.User;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Prompts;
using SportsData.Core.Common;
using SportsData.Core.Processing;

namespace SportsData.Api.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode)
        {
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ITeamCardService, TeamCardService>();
            services.AddScoped<IGetTeamCardQueryHandler, GetTeamCardQueryHandler>();
            services.AddScoped<IProvideCanonicalData, CanonicalDataProvider>();
            services.AddScoped<ILeagueService, LeagueService>();
            services.AddScoped<ICreateLeagueCommandHandler, CreateLeagueCommandHandler>();
            services.AddScoped<IJoinLeagueCommandHandler, JoinLeagueCommandHandler>();
            services.AddScoped<MatchupScheduler>();
            services.AddScoped<MatchupPreviewGenerator>();
            services.AddScoped<IScheduleGroupWeekMatchups, MatchupScheduleProcessor>();
            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();
            services.AddSingleton<MatchupPreviewPromptProvider>();

            services.AddScoped<IPickService, PickService>();
            services.AddScoped<ISubmitUserPickCommandHandler, SubmitUserPickCommandHandler>();
            services.AddScoped<IGetUserPicksQueryHandler, GetUserPicksQueryHandler>();

            services.AddScoped<IMessageboardService, MessageboardService>();

            //services.AddScoped<IProvideAiCommunication, OllamaClient>();
            //services.AddSingleton<OllamaClientConfig>();

            return services;
        }

        public static IServiceProvider ConfigureHangfireJobs(
            this IServiceProvider services,
            Sport mode)
        {
            var serviceScope = services.CreateScope();

            var recurringJobManager = serviceScope.ServiceProvider
                .GetRequiredService<IRecurringJobManager>();

            recurringJobManager.AddOrUpdate<MatchupScheduler>(
                nameof(MatchupScheduler),
                job => job.ExecuteAsync(),
                Cron.Weekly);

            recurringJobManager.AddOrUpdate<MatchupPreviewGenerator>(
                nameof(MatchupPreviewGenerator),
                job => job.ExecuteAsync(),
                Cron.Weekly);

            return services;
        }
    }
}
