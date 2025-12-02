using Hangfire;

using SportsData.Api.Application.Admin;
using SportsData.Api.Application.Admin.SyntheticPicks;
using SportsData.Api.Application.AI;
using SportsData.Api.Application.Contests;
using SportsData.Api.Application.Jobs;
using SportsData.Api.Application.Previews;
using SportsData.Api.Application.Processors;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Application.UI.Articles;
using SportsData.Api.Application.UI.Conferences;
using SportsData.Api.Application.UI.Contest;
using SportsData.Api.Application.UI.Leaderboard;
using SportsData.Api.Application.UI.Leagues;
using SportsData.Api.Application.UI.Leagues.JoinLeague;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage;
using SportsData.Api.Application.UI.Map;
using SportsData.Api.Application.UI.Matchups;
using SportsData.Api.Application.UI.Messageboard;
using SportsData.Api.Application.UI.Picks;
using SportsData.Api.Application.UI.Picks.PicksPage;
using SportsData.Api.Application.UI.Rankings;
using SportsData.Api.Application.UI.TeamCard;
using SportsData.Api.Application.UI.TeamCard.Handlers;
using SportsData.Api.Application.User;
using SportsData.Api.Config;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Api.Infrastructure.Prompts;
using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;

namespace SportsData.Api.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode)
        {
            services.AddDataPersistenceExternal();

            services.AddScoped<IConferenceService, ConferenceService>();
            services.AddScoped<ICreateLeagueCommandHandler, CreateLeagueCommandHandler>();
            services.AddScoped<IGenerateMatchupPreviews, MatchupPreviewProcessor>();
            services.AddScoped<IGetTeamCardQueryHandler, GetTeamCardQueryHandler>();
            services.AddScoped<IGetUserPicksQueryHandler, GetUserPicksQueryHandler>();
            services.AddScoped<IJoinLeagueCommandHandler, JoinLeagueCommandHandler>();
            services.AddScoped<ILeagueService, LeagueService>();
            services.AddScoped<IMatchupService, MatchupService>();
            services.AddScoped<IMessageboardService, MessageboardService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IPickService, PickService>();
            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();
            services.AddScoped<IProvideCanonicalData, CanonicalDataProvider>();
            services.AddSingleton<CanonicalDataQueryProvider>();
            services.AddScoped<IProvideCanonicalAdminData, CanonicalAdminDataProvider>();
            services.AddSingleton<CanonicalAdminDataQueryProvider>();
            services.AddScoped<IScheduleGroupWeekMatchups, MatchupScheduleProcessor>();
            services.AddScoped<IScoreContests, ContestScoringProcessor>();
            services.AddScoped<ISubmitUserPickCommandHandler, SubmitUserPickCommandHandler>();

            services.AddScoped<IStatFormattingService, StatFormattingService>();
            services.AddScoped<ITeamCardService, TeamCardService>();

            services.AddScoped<IUserService, UserService>();
            services.AddScoped<MatchupPreviewGenerator>();
            services.AddScoped<MatchupScheduler>();
            services.AddSingleton<MatchupPreviewPromptProvider>();
            services.AddSingleton<GameRecapPromptProvider>();
            services.AddScoped<ContestScoringJob>();

            services.AddScoped<ContestRecapJob>();
            services.AddScoped<ContestRecapProcessor>();

            services.AddScoped<IPickScoringService, PickScoringService>();
            services.AddScoped<ILeaderboardService, LeaderboardService>();
            services.AddScoped<IAdminService, AdminService>();
            services.AddScoped<IAiService, AiService>();

            // Synthetic pick services
            services.AddSingleton<ISyntheticPickStyleProvider, SyntheticPickStyleProvider>();
            services.AddScoped<ISyntheticPickService, SyntheticPickService>();

            services.AddScoped<IContestService, ContestService>();
            services.AddScoped<IRankingsService, RankingsService>();

            services.AddScoped<IPreviewService, PreviewService>();

            services.AddScoped<IMapService, MapService>();

            services.AddScoped<IArticleService, ArticleService>();

            return services;
        }

        public static IServiceProvider ConfigureHangfireJobs(
            this IServiceProvider services,
            Sport mode)
        {
            var serviceScope = services.CreateScope();

            var recurringJobManager = serviceScope.ServiceProvider
                .GetRequiredService<IRecurringJobManager>();

            recurringJobManager.RemoveIfExists(nameof(ContestRecapJob));
            recurringJobManager.RemoveIfExists(nameof(ContestScoringJob));
            recurringJobManager.RemoveIfExists(nameof(MatchupPreviewGenerator));
            recurringJobManager.RemoveIfExists(nameof(MatchupScheduler));

            recurringJobManager.AddOrUpdate<ContestRecapJob>(
                nameof(ContestRecapJob),
                job => job.ExecuteAsync(),
                Cron.Weekly);

            recurringJobManager.AddOrUpdate<ContestScoringJob>(
                nameof(ContestScoringJob),
                job => job.ExecuteAsync(),
                Cron.Weekly);

            recurringJobManager.AddOrUpdate<MatchupPreviewGenerator>(
                nameof(MatchupPreviewGenerator),
                job => job.ExecuteAsync(),
                Cron.Weekly);

            recurringJobManager.AddOrUpdate<MatchupScheduler>(
                nameof(MatchupScheduler),
                job => job.ExecuteAsync(),
                Cron.Weekly);

            return services;
        }
    }
}
