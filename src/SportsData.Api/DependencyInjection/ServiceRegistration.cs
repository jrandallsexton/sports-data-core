using Hangfire;

using FluentValidation;

using SportsData.Api.Application.Admin.Commands.BackfillLeagueScores;
using SportsData.Api.Application.Admin.Commands.GenerateGameRecap;
using SportsData.Api.Application.Admin.Commands.GenerateLoadTest;
using SportsData.Api.Application.Admin.Commands.RefreshAiExistence;
using SportsData.Api.Application.Admin.Commands.SendTestPushNotification;
using SportsData.Api.Application.Admin.Commands.UpsertMatchupPreview;
using SportsData.Api.Application.Admin.Queries.AuditAi;
using SportsData.Api.Application.Admin.Queries.GetAiResponse;
using SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutCompetitors;
using SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutDrives;
using SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutMetrics;
using SportsData.Api.Application.Admin.Queries.GetCompetitionsWithoutPlays;
using SportsData.Api.Application.Admin.SyntheticPicks;
using SportsData.Api.Application.Jobs;
using SportsData.Api.Application.Previews;
using SportsData.Api.Application.Processors;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Application.UI.Articles.Queries.GetArticleById;
using SportsData.Api.Application.UI.Articles.Queries.GetArticles;
using SportsData.Api.Application.Franchises.Queries.GetFranchises;
using SportsData.Api.Application.Franchises.Queries.GetFranchiseById;
using SportsData.Api.Application.Franchises.Seasons.Queries.GetFranchiseSeasons;
using SportsData.Api.Application.Franchises.Seasons.Queries.GetFranchiseSeasonById;
using SportsData.Api.Application.Franchises.Seasons.Contests;
using SportsData.Api.Application.Contests.Queries.GetContestById;
using SportsData.Api.Application.Venues.Queries.GetVenues;
using SportsData.Api.Application.Venues.Queries.GetVenueById;
using SportsData.Api.Application.UI.Conferences.Queries.GetConferenceNamesAndSlugs;
using SportsData.Api.Infrastructure.Refs;
using SportsData.Api.Application.Admin.Commands.ReenrichContest;
using SportsData.Api.Application.UI.Contest.Commands.FinalizeContest;
using SportsData.Api.Application.UI.Contest.Commands.RefreshContest;
using SportsData.Api.Application.UI.Contest.Commands.RefreshContestMedia;
using SportsData.Api.Application.UI.Contest.Commands.SubmitContestPredictions;
using SportsData.Api.Application.UI.Contest.Queries.GetContestOverview;
using SportsData.Api.Application.UI.Contest.Queries.GetContestPlayLog;
using SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboard;
using SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboardWidget;
using SportsData.Api.Application.UI.Leagues.Commands.AddMatchup;
using SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague;
using SportsData.Api.Application.UI.Leagues.Commands.DeleteLeague;
using SportsData.Api.Application.UI.Leagues.Commands.GenerateLeagueWeekPreviews;
using SportsData.Api.Application.UI.Leagues.Commands.InviteUserToLeague;
using SportsData.Api.Application.UI.Leagues.Commands.JoinLeague;
using SportsData.Api.Application.UI.Leagues.Commands.SendLeagueInvite;
using SportsData.Api.Application.UI.Leagues.Queries.GetInviteableUsers;
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
using SportsData.Api.Application.UI.Season.Queries.GetSeasonOverview;
using SportsData.Api.Application.UI.Picks.Commands.SubmitPick;
using SportsData.Api.Application.UI.Picks.Queries.GetPickAccuracyByWeek;
using SportsData.Api.Application.UI.Picks.Queries.GetPickRecordWidget;
using SportsData.Api.Application.UI.Picks.Queries.GetUserPicksByGroupAndWeek;
using SportsData.Api.Application.UI.Rankings.Queries.GetPollRankingsByWeek;
using SportsData.Api.Application.UI.Rankings.Queries.GetRankingsByPollSeasonWeekId;
using SportsData.Api.Application.UI.Rankings.Queries.GetRankingsByPollWeek;
using SportsData.Api.Application.UI.Rankings.Queries.GetRankingsBySeasonYear;
using SportsData.Api.Application.UI.TeamCard;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamCard;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamMetrics;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamFinalizedGames;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamStatistics;
using SportsData.Api.Application.User;
using SportsData.Api.Application.User.Commands.DeleteAccount;
using SportsData.Api.Application.User.Commands.UpdateDisplayName;
using SportsData.Api.Application.User.Commands.UpdateNotificationPreferences;
using SportsData.Api.Application.User.Commands.UpdateUsername;
using SportsData.Api.Application.User.Commands.UpdateUserTimezone;
using SportsData.Api.Application.User.Commands.UpsertUser;
using SportsData.Api.Application.User.Queries.GetMe;
using SportsData.Api.Application.User.Queries.GetNotificationPreferences;
using SportsData.Api.Config;
using SportsData.Api.Infrastructure.Auth;
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
            services.AddScoped<ICreateFootballNcaaLeagueCommandHandler, CreateFootballNcaaLeagueCommandHandler>();
            services.AddScoped<ICreateFootballNflLeagueCommandHandler, CreateFootballNflLeagueCommandHandler>();
            services.AddScoped<ICreateBaseballMlbLeagueCommandHandler, CreateBaseballMlbLeagueCommandHandler>();
            services.AddScoped<IDeleteLeagueCommandHandler, DeleteLeagueCommandHandler>();
            services.AddScoped<IGenerateLeagueWeekPreviewsCommandHandler, GenerateLeagueWeekPreviewsCommandHandler>();
            services.AddScoped<IJoinLeagueCommandHandler, JoinLeagueCommandHandler>();
            services.AddScoped<ISendLeagueInviteCommandHandler, SendLeagueInviteCommandHandler>();
            services.AddScoped<IInviteUserToLeagueCommandHandler, InviteUserToLeagueCommandHandler>();

            // League Queries
            services.AddScoped<IGetLeagueByIdQueryHandler, GetLeagueByIdQueryHandler>();
            services.AddScoped<IGetInviteableUsersQueryHandler, GetInviteableUsersQueryHandler>();
            services.AddScoped<IGetLeagueScoresByWeekQueryHandler, GetLeagueScoresByWeekQueryHandler>();
            services.AddScoped<IGetLeagueWeekMatchupsQueryHandler, GetLeagueWeekMatchupsQueryHandler>();
            services.AddScoped<IGetLeagueWeekOverviewQueryHandler, GetLeagueWeekOverviewQueryHandler>();
            services.AddScoped<IGetPublicLeaguesQueryHandler, GetPublicLeaguesQueryHandler>();
            services.AddScoped<IGetUserLeaguesQueryHandler, GetUserLeaguesQueryHandler>();

            // Public Results Queries
            services.AddScoped<
                SportsData.Api.Application.UI.Results.Queries.GetSeasonResults.IGetSeasonResultsQueryHandler,
                SportsData.Api.Application.UI.Results.Queries.GetSeasonResults.GetSeasonResultsQueryHandler>();

            // Admin Commands
            services.AddScoped<IBackfillLeagueScoresCommandHandler, BackfillLeagueScoresCommandHandler>();
            services.AddScoped<IGenerateGameRecapCommandHandler, GenerateGameRecapCommandHandler>();
            services.AddScoped<IGenerateLoadTestCommandHandler, GenerateLoadTestCommandHandler>();
            services.AddScoped<IReenrichContestCommandHandler, ReenrichContestCommandHandler>();
            services.AddScoped<IRefreshAiExistenceCommandHandler, RefreshAiExistenceCommandHandler>();
            services.AddScoped<ISendTestPushNotificationCommandHandler, SendTestPushNotificationCommandHandler>();
            services.AddScoped<
                SportsData.Api.Application.UI.Devices.Commands.RegisterDevice.IRegisterDeviceCommandHandler,
                SportsData.Api.Application.UI.Devices.Commands.RegisterDevice.RegisterDeviceCommandHandler>();
            services.AddScoped<
                SportsData.Api.Application.UI.Devices.Commands.UnregisterDevice.IUnregisterDeviceCommandHandler,
                SportsData.Api.Application.UI.Devices.Commands.UnregisterDevice.UnregisterDeviceCommandHandler>();
            services.AddScoped<IUpsertMatchupPreviewCommandHandler, UpsertMatchupPreviewCommandHandler>();

            // Notifications
            services.AddScoped<SportsData.Api.Infrastructure.Notifications.IPushNotificationSender,
                SportsData.Api.Infrastructure.Notifications.FirebasePushNotificationSender>();

            // Admin Jobs
            services.AddScoped<SportsData.Api.Application.Admin.Jobs.IPublishLoadTestEventsJob, SportsData.Api.Application.Admin.Jobs.PublishLoadTestEventsJob>();

            // Admin Queries
            services.AddScoped<IAuditAiQueryHandler, AuditAiQueryHandler>();
            services.AddScoped<IGetAiResponseQueryHandler, GetAiResponseQueryHandler>();
            services.AddScoped<IGetCompetitionsWithoutCompetitorsQueryHandler, GetCompetitionsWithoutCompetitorsQueryHandler>();
            services.AddScoped<IGetCompetitionsWithoutPlaysQueryHandler, GetCompetitionsWithoutPlaysQueryHandler>();
            services.AddScoped<IGetCompetitionsWithoutDrivesQueryHandler, GetCompetitionsWithoutDrivesQueryHandler>();
            services.AddScoped<IGetCompetitionsWithoutMetricsQueryHandler, GetCompetitionsWithoutMetricsQueryHandler>();
            services.AddScoped<SportsData.Api.Application.Admin.Queries.GetMatchupPreview.IGetMatchupPreviewQueryHandler,
                SportsData.Api.Application.Admin.Queries.GetMatchupPreview.GetMatchupPreviewQueryHandler>();
            services.AddScoped<SportsData.Api.Application.Admin.Queries.GetMatchupForContest.IGetMatchupForContestQueryHandler,
                SportsData.Api.Application.Admin.Queries.GetMatchupForContest.GetMatchupForContestQueryHandler>();
            services.AddScoped<SportsData.Api.Application.Admin.Queries.GetLeagueWeekContests.IGetLeagueWeekContestsQueryHandler,
                SportsData.Api.Application.Admin.Queries.GetLeagueWeekContests.GetLeagueWeekContestsQueryHandler>();

            // Analytics Queries
            services.AddScoped<IGetFranchiseSeasonMetricsQueryHandler, GetFranchiseSeasonMetricsQueryHandler>();

            // Articles Queries
            services.AddScoped<IGetArticlesQueryHandler, GetArticlesQueryHandler>();
            services.AddScoped<IGetArticleByIdQueryHandler, GetArticleByIdQueryHandler>();

            // Franchises Queries
            services.AddScoped<IGetFranchisesQueryHandler, GetFranchisesQueryHandler>();
            services.AddScoped<IGetFranchiseByIdQueryHandler, GetFranchiseByIdQueryHandler>();
            services.AddScoped<IGetFranchiseSeasonsQueryHandler, GetFranchiseSeasonsQueryHandler>();
            services.AddScoped<IGetFranchiseSeasonByIdQueryHandler, GetFranchiseSeasonByIdQueryHandler>();
            services.AddScoped<IGetSeasonContestsQueryHandler, GetSeasonContestsQueryHandler>();
            services.AddScoped<IGetContestByIdQueryHandler, GetContestByIdQueryHandler>();

            // Venues Queries
            services.AddScoped<IGetVenuesQueryHandler, GetVenuesQueryHandler>();
            services.AddScoped<IGetVenueByIdQueryHandler, GetVenueByIdQueryHandler>();

            // Conferences Queries
            services.AddScoped<IGetConferenceNamesAndSlugsQueryHandler, GetConferenceNamesAndSlugsQueryHandler>();

            // Contest Commands
            services.AddScoped<IRefreshContestCommandHandler, RefreshContestCommandHandler>();
            services.AddScoped<IRefreshContestMediaCommandHandler, RefreshContestMediaCommandHandler>();
            services.AddScoped<IFinalizeContestCommandHandler, FinalizeContestCommandHandler>();
            services.AddScoped<ISubmitContestPredictionsCommandHandler, SubmitContestPredictionsCommandHandler>();

            // Contest Queries
            services.AddScoped<IGetContestOverviewQueryHandler, GetContestOverviewQueryHandler>();
            services.AddScoped<IGetContestPlayLogQueryHandler, GetContestPlayLogQueryHandler>();

            // Season Queries
            services.AddScoped<IGetSeasonOverviewQueryHandler, GetSeasonOverviewQueryHandler>();

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
            services.AddSingleton<CanonicalDataQueryProvider>();
            services.AddScoped<IProvideCanonicalAdminData, CanonicalAdminDataProvider>();
            services.AddSingleton<CanonicalAdminDataQueryProvider>();
            services.AddScoped<IScheduleGroupWeekMatchups, MatchupScheduleProcessor>();
            services.AddScoped<IBootstrapLeagueMatchups, BootstrapLeagueMatchupsProcessor>();
            services.AddScoped<IScorePicks, PickScoringProcessor>();
            services.AddScoped<IScoreLeagueWeeks, LeagueWeekScoringProcessor>();
            
            // HATEOAS Ref Generator (external API)
            services.AddSingleton<IGenerateApiResourceRefs, ApiResourceRefGenerator>();

            // TeamCard Queries
            services.AddScoped<IGetTeamCardQueryHandler, GetTeamCardQueryHandler>();
            services.AddScoped<IGetTeamFinalizedGamesQueryHandler, GetTeamFinalizedGamesQueryHandler>();
            services.AddScoped<IGetTeamStatisticsQueryHandler, GetTeamStatisticsQueryHandler>();
            services.AddScoped<IGetTeamMetricsQueryHandler, GetTeamMetricsQueryHandler>();
            services.AddScoped<IStatFormattingService, StatFormattingService>();

            // User Commands
            services.AddScoped<IUpsertUserCommandHandler, UpsertUserCommandHandler>();
            services.AddScoped<IUpdateUserTimezoneCommandHandler, UpdateUserTimezoneCommandHandler>();
            services.AddScoped<IUpdateUsernameCommandHandler, UpdateUsernameCommandHandler>();
            services.AddScoped<IUpdateDisplayNameCommandHandler, UpdateDisplayNameCommandHandler>();
            services.AddScoped<IDeleteAccountCommandHandler, DeleteAccountCommandHandler>();
            services.AddScoped<IUpdateNotificationPreferencesCommandHandler, UpdateNotificationPreferencesCommandHandler>();
            services.AddSingleton<IFirebaseUserAdmin, FirebaseUserAdmin>();

            // User Validators
            services.AddValidatorsFromAssemblyContaining<Program>();

            // User Queries
            services.AddScoped<IGetMeQueryHandler, GetMeQueryHandler>();
            services.AddScoped<IGetNotificationPreferencesQueryHandler, GetNotificationPreferencesQueryHandler>();

            services.AddScoped<IUserService, UserService>();
            services.AddScoped<MatchupPreviewGenerator>();
            services.AddScoped<MatchupScheduler>();
            services.AddSingleton<MatchupPreviewPromptProvider>();
            services.AddSingleton<GameRecapPromptProvider>();
            services.AddScoped<PickScoringJob>();
            services.AddScoped<LeagueWeekScoringJob>();

            // TODO: Restore after Contest processing is refactored
            // services.AddScoped<ContestRecapJob>();
            // services.AddScoped<ContestRecapProcessor>();

            services.AddScoped<IPickScoringService, PickScoringService>();
            services.AddScoped<ILeagueWeekScoringService, LeagueWeekScoringService>();
            services.AddScoped<IPickScoringAudit, PickScoringAuditProcessor>();
            services.AddScoped<PickScoringAuditJob>();

            // Synthetic pick services (required by other services)
            services.AddSingleton<ISyntheticPickStyleProvider, SyntheticPickStyleProvider>();
            services.AddScoped<ISyntheticPickService, SyntheticPickService>();

            // Rankings Queries
            services.AddScoped<IGetRankingsBySeasonYearQueryHandler, GetRankingsBySeasonYearQueryHandler>();
            services.AddScoped<IGetRankingsByPollWeekQueryHandler, GetRankingsByPollWeekQueryHandler>();
            services.AddScoped<IGetRankingsByPollSeasonWeekIdQueryHandler, GetRankingsByPollSeasonWeekIdQueryHandler>();
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

            // Daily backstop. Primary scoring trigger is event-driven
            // (Producer ContestCompleted → API ContestCompletedHandler enqueues
            // ContestScoringProcessor); this catches events lost in transit.
            recurringJobManager.AddOrUpdate<PickScoringJob>(
                nameof(PickScoringJob),
                job => job.ExecuteAsync(),
                Cron.Daily(9));

            // Daily backstop. Primary trigger is the tail call inside
            // ContestScoringProcessor; this catches league weeks where the
            // tail leaderboard scoring failed. 15-min stagger so it runs
            // after ContestScoringJob's enqueues have had time to process.
            recurringJobManager.AddOrUpdate<LeagueWeekScoringJob>(
                nameof(LeagueWeekScoringJob),
                job => job.ExecuteAsync(),
                Cron.Daily(9, 15));

            recurringJobManager.AddOrUpdate<MatchupPreviewGenerator>(
                nameof(MatchupPreviewGenerator),
                job => job.ExecuteAsync(),
                Cron.Weekly);

            // Daily primary trigger. Can't be event-driven — matchups must
            // be generated BEFORE games happen. Daily is sufficient since
            // week boundaries move at most once per week per sport.
            recurringJobManager.AddOrUpdate<MatchupScheduler>(
                nameof(MatchupScheduler),
                job => job.ExecuteAsync(),
                Cron.Daily(6));

            // Per-sport historical audit of previously-scored picks. Catches
            // (a) picks scored against a contest that later finalized to a
            // different result and (b) picks still scored against an
            // unfinalized contest. Runs before PickScoringJob(9) so any
            // ScoredAt resets land in time for the daily rescore.
            //
            // Stagger by 15 min per sport so a single sport's audit owns
            // its window in Seq — pods are separate so the lack of stagger
            // wouldn't actually collide, but staggering makes "which sport
            // is misbehaving" trivial to identify.
            recurringJobManager.AddOrUpdate<PickScoringAuditJob>(
                "PickScoringAudit-FootballNcaa",
                job => job.ExecuteAsync(Sport.FootballNcaa),
                "0 2 * * *");

            recurringJobManager.AddOrUpdate<PickScoringAuditJob>(
                "PickScoringAudit-FootballNfl",
                job => job.ExecuteAsync(Sport.FootballNfl),
                "15 2 * * *");

            recurringJobManager.AddOrUpdate<PickScoringAuditJob>(
                "PickScoringAudit-BaseballMlb",
                job => job.ExecuteAsync(Sport.BaseballMlb),
                "30 2 * * *");

            return services;
        }
    }
}
