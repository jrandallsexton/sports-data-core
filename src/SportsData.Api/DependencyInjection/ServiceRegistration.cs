using Hangfire;

using FluentValidation;

using SportsData.Api.Application.Admin;
using SportsData.Api.Application.Admin.Commands.BackfillLeagueScores;
using SportsData.Api.Application.Admin.Commands.UpsertMatchupPreview;
using SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutCompetitors;
using SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutDrives;
using SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutMetrics;
using SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutPlays;
using SportsData.Api.Application.Admin.SyntheticPicks;
using SportsData.Api.Application.AI;
using SportsData.Api.Application.Contests;
using SportsData.Api.Application.Jobs;
using SportsData.Api.Application.Previews;
using SportsData.Api.Application.Processors;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Application.UI.Articles.Queries.GetArticleById;
using SportsData.Api.Application.UI.Articles.Queries.GetArticles;
using SportsData.Api.Application.UI.Conferences.Queries.GetConferenceNamesAndSlugs;
using SportsData.Api.Application.UI.Contest.Commands.RefreshContest;
using SportsData.Api.Application.UI.Contest.Commands.RefreshContestMedia;
using SportsData.Api.Application.UI.Contest.Commands.SubmitContestPredictions;
using SportsData.Api.Application.UI.Contest.Queries.GetContestOverview;
using SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboard;
using SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboardWidget;
using SportsData.Api.Application.UI.Leagues.Commands.AddMatchup;
using SportsData.Api.Application.UI.Leagues.Commands.CreateLeague;
using SportsData.Api.Application.UI.Leagues.Commands.DeleteLeague;
using SportsData.Api.Application.UI.Leagues.Commands.GenerateLeagueWeekPreviews;
using SportsData.Api.Application.UI.Leagues.Commands.JoinLeague;
using SportsData.Api.Application.UI.Leagues.Commands.SendLeagueInvite;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueById;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueScoresByWeek;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekOverview;
using SportsData.Api.Application.UI.Leagues.Queries.GetPublicLeagues;
using SportsData.Api.Application.UI.Leagues.Queries.GetUserLeagues;
using SportsData.Api.Application.UI.Analytics.Queries.GetFranchiseSeasonMetrics;
using SportsData.Api.Application.UI.Map.Queries.GetMapMatchups;
using SportsData.Api.Application.UI.Matchups.Queries.GetMatchupPreview;
using SportsData.Api.Application.UI.Messageboard.Commands.CreateReply;
using SportsData.Api.Application.UI.Messageboard.Commands.CreateThread;
using SportsData.Api.Application.UI.Messageboard.Commands.ToggleReaction;
using SportsData.Api.Application.UI.Messageboard.Queries.GetReplies;
using SportsData.Api.Application.UI.Messageboard.Queries.GetThreads;
using SportsData.Api.Application.UI.Messageboard.Queries.GetThreadsByUserGroups;
using SportsData.Api.Application.UI.Picks.Commands.SubmitPick;
using SportsData.Api.Application.UI.Picks.Queries.GetPickAccuracyByWeek;
using SportsData.Api.Application.UI.Picks.Queries.GetPickRecordWidget;
using SportsData.Api.Application.UI.Picks.Queries.GetUserPicksByGroupAndWeek;
using SportsData.Api.Application.UI.Rankings.Queries.GetPollRankingsByWeek;
using SportsData.Api.Application.UI.Rankings.Queries.GetRankingsByPollWeek;
using SportsData.Api.Application.UI.Rankings.Queries.GetRankingsBySeasonYear;
using SportsData.Api.Application.UI.TeamCard;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamCard;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamMetrics;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamStatistics;
using SportsData.Api.Application.User;
using SportsData.Api.Application.User.Commands.UpsertUser;
using SportsData.Api.Application.User.Queries.GetMe;
using SportsData.Api.Config;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Api.Infrastructure.Prompts;
using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode)
        {
            services.AddDataPersistenceExternal();

            // League Commands
            services.AddScoped<IAddMatchupCommandHandler, AddMatchupCommandHandler>();
            services.AddScoped<ICreateLeagueCommandHandler, CreateLeagueCommandHandler>();
            services.AddScoped<IDeleteLeagueCommandHandler, DeleteLeagueCommandHandler>();
            services.AddScoped<IGenerateLeagueWeekPreviewsCommandHandler, GenerateLeagueWeekPreviewsCommandHandler>();
            services.AddScoped<IJoinLeagueCommandHandler, JoinLeagueCommandHandler>();
            services.AddScoped<ISendLeagueInviteCommandHandler, SendLeagueInviteCommandHandler>();

            // League Queries
            services.AddScoped<IGetLeagueByIdQueryHandler, GetLeagueByIdQueryHandler>();
            services.AddScoped<IGetLeagueScoresByWeekQueryHandler, GetLeagueScoresByWeekQueryHandler>();
            services.AddScoped<IGetLeagueWeekMatchupsQueryHandler, GetLeagueWeekMatchupsQueryHandler>();
            services.AddScoped<IGetLeagueWeekOverviewQueryHandler, GetLeagueWeekOverviewQueryHandler>();
            services.AddScoped<IGetPublicLeaguesQueryHandler, GetPublicLeaguesQueryHandler>();
            services.AddScoped<IGetUserLeaguesQueryHandler, GetUserLeaguesQueryHandler>();

            // Admin Commands
            services.AddScoped<IBackfillLeagueScoresCommandHandler, BackfillLeagueScoresCommandHandler>();
            services.AddScoped<IUpsertMatchupPreviewCommandHandler, UpsertMatchupPreviewCommandHandler>();

            // Admin Queries
            services.AddScoped<IGetCompetitionsWithoutCompetitorsQueryHandler, GetCompetitionsWithoutCompetitorsQueryHandler>();
            services.AddScoped<IGetCompetitionsWithoutPlaysQueryHandler, GetCompetitionsWithoutPlaysQueryHandler>();
            services.AddScoped<IGetCompetitionsWithoutDrivesQueryHandler, GetCompetitionsWithoutDrivesQueryHandler>();
            services.AddScoped<IGetCompetitionsWithoutMetricsQueryHandler, GetCompetitionsWithoutMetricsQueryHandler>();
            services.AddScoped<SportsData.Api.Application.Admin.Queries.GetMatchupPreview.IGetMatchupPreviewQueryHandler,
                SportsData.Api.Application.Admin.Queries.GetMatchupPreview.GetMatchupPreviewQueryHandler>();

            // Analytics Queries
            services.AddScoped<IGetFranchiseSeasonMetricsQueryHandler, GetFranchiseSeasonMetricsQueryHandler>();

            // Articles Queries
            services.AddScoped<IGetArticlesQueryHandler, GetArticlesQueryHandler>();
            services.AddScoped<IGetArticleByIdQueryHandler, GetArticleByIdQueryHandler>();

            // Conferences Queries
            services.AddScoped<IGetConferenceNamesAndSlugsQueryHandler, GetConferenceNamesAndSlugsQueryHandler>();

            // Contest Commands
            services.AddScoped<IRefreshContestCommandHandler, RefreshContestCommandHandler>();
            services.AddScoped<IRefreshContestMediaCommandHandler, RefreshContestMediaCommandHandler>();
            services.AddScoped<ISubmitContestPredictionsCommandHandler, SubmitContestPredictionsCommandHandler>();

            // Contest Queries
            services.AddScoped<IGetContestOverviewQueryHandler, GetContestOverviewQueryHandler>();

            // Leaderboard Queries
            services.AddScoped<IGetLeaderboardQueryHandler, GetLeaderboardQueryHandler>();
            services.AddScoped<IGetLeaderboardWidgetQueryHandler, GetLeaderboardWidgetQueryHandler>();

            // Matchups Queries
            services.AddScoped<IGetMatchupPreviewQueryHandler, GetMatchupPreviewQueryHandler>();

            // Messageboard Commands
            services.AddScoped<ICreateThreadCommandHandler, CreateThreadCommandHandler>();
            services.AddScoped<ICreateReplyCommandHandler, CreateReplyCommandHandler>();
            services.AddScoped<IToggleReactionCommandHandler, ToggleReactionCommandHandler>();

            // Messageboard Queries
            services.AddScoped<IGetThreadsByUserGroupsQueryHandler, GetThreadsByUserGroupsQueryHandler>();
            services.AddScoped<IGetThreadsQueryHandler, GetThreadsQueryHandler>();
            services.AddScoped<IGetRepliesQueryHandler, GetRepliesQueryHandler>();

            // Picks Commands
            services.AddScoped<ISubmitPickCommandHandler, SubmitPickCommandHandler>();

            // Picks Queries
            services.AddScoped<IGetUserPicksByGroupAndWeekQueryHandler, GetUserPicksByGroupAndWeekQueryHandler>();
            services.AddScoped<IGetPickRecordWidgetQueryHandler, GetPickRecordWidgetQueryHandler>();
            services.AddScoped<IGetPickAccuracyByWeekQueryHandler, GetPickAccuracyByWeekQueryHandler>();

            services.AddScoped<IGenerateMatchupPreviews, MatchupPreviewProcessor>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();
            services.AddScoped<IProvideCanonicalData, CanonicalDataProvider>();
            services.AddSingleton<CanonicalDataQueryProvider>();
            services.AddScoped<IProvideCanonicalAdminData, CanonicalAdminDataProvider>();
            services.AddSingleton<CanonicalAdminDataQueryProvider>();
            services.AddScoped<IScheduleGroupWeekMatchups, MatchupScheduleProcessor>();
            services.AddScoped<IScoreContests, ContestScoringProcessor>();

            // TeamCard Queries
            services.AddScoped<IGetTeamCardQueryHandler, GetTeamCardQueryHandler>();
            services.AddScoped<IGetTeamStatisticsQueryHandler, GetTeamStatisticsQueryHandler>();
            services.AddScoped<IGetTeamMetricsQueryHandler, GetTeamMetricsQueryHandler>();
            services.AddScoped<IStatFormattingService, StatFormattingService>();

            // User Commands
            services.AddScoped<IUpsertUserCommandHandler, UpsertUserCommandHandler>();
            
            // User Validators
            services.AddValidatorsFromAssemblyContaining<Program>();

            // User Queries
            services.AddScoped<IGetMeQueryHandler, GetMeQueryHandler>();

            services.AddScoped<IUserService, UserService>();
            services.AddScoped<MatchupPreviewGenerator>();
            services.AddScoped<MatchupScheduler>();
            services.AddSingleton<MatchupPreviewPromptProvider>();
            services.AddSingleton<GameRecapPromptProvider>();
            services.AddScoped<ContestScoringJob>();
            services.AddScoped<LeagueWeekScoringJob>();

            services.AddScoped<ContestRecapJob>();
            services.AddScoped<ContestRecapProcessor>();

            services.AddScoped<IPickScoringService, PickScoringService>();
            services.AddScoped<ILeagueWeekScoringService, LeagueWeekScoringService>();
            services.AddScoped<IAdminService, AdminService>();
            services.AddScoped<IAiService, AiService>();

            // Synthetic pick services
            services.AddSingleton<ISyntheticPickStyleProvider, SyntheticPickStyleProvider>();
            services.AddScoped<ISyntheticPickService, SyntheticPickService>();

            // Rankings Queries
            services.AddScoped<IGetRankingsBySeasonYearQueryHandler, GetRankingsBySeasonYearQueryHandler>();
            services.AddScoped<IGetRankingsByPollWeekQueryHandler, GetRankingsByPollWeekQueryHandler>();
            services.AddScoped<IGetPollRankingsByWeekQueryHandler, GetPollRankingsByWeekQueryHandler>();

            services.AddScoped<IPreviewService, PreviewService>();

            // Map Queries
            services.AddScoped<IGetMapMatchupsQueryHandler, GetMapMatchupsQueryHandler>();

            return services;
        }

        public static IServiceProvider ConfigureHangfireJobs(
            this IServiceProvider services,
            Sport mode)
        {
            var serviceScope = services.CreateScope();

            var recurringJobManager = serviceScope.ServiceProvider
                .GetRequiredService<IRecurringJobManager>();

            recurringJobManager.AddOrUpdate<ContestRecapJob>(
                nameof(ContestRecapJob),
                job => job.ExecuteAsync(),
                Cron.Weekly);

            recurringJobManager.AddOrUpdate<ContestScoringJob>(
                nameof(ContestScoringJob),
                job => job.ExecuteAsync(),
                Cron.Weekly);

            recurringJobManager.AddOrUpdate<LeagueWeekScoringJob>(
                nameof(LeagueWeekScoringJob),
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
