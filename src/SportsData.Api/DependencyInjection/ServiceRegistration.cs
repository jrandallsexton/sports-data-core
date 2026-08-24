using Hangfire;

using FluentValidation;

using SportsData.Api.Application.Admin.Commands.BackfillLeagueScores;
using SportsData.Api.Application.Athletes.Queries.GetAthleteDetails;
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
using SportsData.Api.Application.PickemGroups;
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
using SportsData.Api.Application.Contests.Queries.GetContestHistory;
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
using SportsData.Api.Application.UI.Leagues.Authorization;
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
using SportsData.Api.Application.User.Commands.UpdateUserOptions;
using SportsData.Api.Application.User.Commands.UpdateUserTimezone;
using SportsData.Api.Application.User.Commands.UpsertUser;
using SportsData.Api.Application.User.Queries.GetMe;
using SportsData.Api.Application.User.Queries.GetNotificationPreferences;
using SportsData.Api.Application.User.Queries.GetUserOptions;
using SportsData.Api.Config;
using SportsData.Api.Infrastructure.Auth;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Api.Infrastructure.Prompts;
using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Contests.Commands.GenerateGameRecap;

namespace SportsData.Api.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(this IServiceCollection services, Sport mode)
        {
            services.AddDataPersistenceExternal();

            // League Commands
            services.AddScoped<IAddMatchupCommandHandler, AddMatchupCommandHandler>();
            services.AddScoped<
                Application.UI.Leagues.Commands.CloneLeague.ICloneLeagueCommandHandler,
                Application.UI.Leagues.Commands.CloneLeague.CloneLeagueCommandHandler>();
            services.AddScoped<ICreateFootballNcaaLeagueCommandHandler, CreateFootballNcaaLeagueCommandHandler>();
            services.AddScoped<ICreateFootballNflLeagueCommandHandler, CreateFootballNflLeagueCommandHandler>();
            services.AddScoped<ICreateBaseballMlbLeagueCommandHandler, CreateBaseballMlbLeagueCommandHandler>();
            services.AddScoped<IDeleteLeagueCommandHandler, DeleteLeagueCommandHandler>();
            services.AddScoped<IGenerateLeagueWeekPreviewsCommandHandler, GenerateLeagueWeekPreviewsCommandHandler>();
            services.AddScoped<IJoinLeagueCommandHandler, JoinLeagueCommandHandler>();
            services.AddScoped<ISendLeagueInviteCommandHandler, SendLeagueInviteCommandHandler>();
            services.AddScoped<IInviteUserToLeagueCommandHandler, InviteUserToLeagueCommandHandler>();
            services.AddScoped<
                Application.UI.Leagues.Commands.AcceptLeagueInvitation.IAcceptLeagueInvitationCommandHandler,
                Application.UI.Leagues.Commands.AcceptLeagueInvitation.AcceptLeagueInvitationCommandHandler>();
            services.AddScoped<
                Application.UI.Leagues.Commands.DeclineLeagueInvitation.IDeclineLeagueInvitationCommandHandler,
                Application.UI.Leagues.Commands.DeclineLeagueInvitation.DeclineLeagueInvitationCommandHandler>();

            // League creation availability gate (config-driven; used by the create
            // guard and the /ui/leagues/creation-availability endpoint).
            services.AddScoped<
                Application.UI.Leagues.ILeagueCreationAvailability,
                Application.UI.Leagues.LeagueCreationAvailability>();

            // League Queries
            // Single authority for by-group authorization — see
            // docs/audit/league-authorization-idor.md.
            services.AddScoped<ILeagueMembershipGuard, LeagueMembershipGuard>();
            services.AddScoped<
                Application.UI.Leagues.Queries.GetPendingInvitations.IGetPendingInvitationsQueryHandler,
                Application.UI.Leagues.Queries.GetPendingInvitations.GetPendingInvitationsQueryHandler>();

            // The ops proxy's named client: redirects are NOT followed (an
            // allowlisted upstream must not bounce the relay to an
            // unvalidated target) and cookies are NOT retained (the pooled
            // handler would otherwise replay upstream Set-Cookie values on
            // later relays to the same host). The relay goes exactly where
            // the allowlist said, carries nothing it wasn't given, or goes
            // nowhere.
            //
            // No BaseAddress on purpose: the target varies PER REQUEST
            // (service x sport mode), so the controller resolves the full
            // URI per call from the same AppConfig-backed CommonConfig keys
            // the typed client factories use, then verifies the resolved
            // base actually owns the final URI.
            services.AddHttpClient(nameof(Application.Admin.AdminOpsProxyController))
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false
                });
            services.AddScoped<IGetLeagueByIdQueryHandler, GetLeagueByIdQueryHandler>();
            services.AddScoped<
                Application.UI.Leagues.Queries.GetLeagueGameDates.IGetLeagueGameDatesQueryHandler,
                Application.UI.Leagues.Queries.GetLeagueGameDates.GetLeagueGameDatesQueryHandler>();
            services.AddScoped<IGetInviteableUsersQueryHandler, GetInviteableUsersQueryHandler>();
            services.AddScoped<IGetLeagueScoresByWeekQueryHandler, GetLeagueScoresByWeekQueryHandler>();
            services.AddScoped<IGetLeagueWeekMatchupsQueryHandler, GetLeagueWeekMatchupsQueryHandler>();
            services.AddScoped<IGetLeagueWeekOverviewQueryHandler, GetLeagueWeekOverviewQueryHandler>();
            services.AddScoped<IGetPublicLeaguesQueryHandler, GetPublicLeaguesQueryHandler>();
            services.AddScoped<IGetUserLeaguesQueryHandler, GetUserLeaguesQueryHandler>();

            // Pick Import (cross-league)
            services.AddScoped<
                Application.UI.Picks.PickImport.Planner.IPickImportPlanner,
                Application.UI.Picks.PickImport.Planner.PickImportPlanner>();
            services.AddScoped<
                Application.UI.Picks.PickImport.Planner.IPickImportPlanService,
                Application.UI.Picks.PickImport.Planner.PickImportPlanService>();
            services.AddScoped<
                Application.UI.Picks.PickImport.Queries.GetPickImportPreview.IGetPickImportPreviewQueryHandler,
                Application.UI.Picks.PickImport.Queries.GetPickImportPreview.GetPickImportPreviewQueryHandler>();
            services.AddScoped<
                Application.UI.Picks.PickImport.Queries.GetPickImportSources.IGetPickImportSourcesQueryHandler,
                Application.UI.Picks.PickImport.Queries.GetPickImportSources.GetPickImportSourcesQueryHandler>();
            services.AddScoped<
                Application.UI.Picks.PickImport.Commands.ImportPicks.IImportPicksCommandHandler,
                Application.UI.Picks.PickImport.Commands.ImportPicks.ImportPicksCommandHandler>();

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
            services.AddScoped<SportsData.Api.Application.Admin.Queries.GetMatchupPreviewCaptures.IGetMatchupPreviewCapturesQueryHandler,
                SportsData.Api.Application.Admin.Queries.GetMatchupPreviewCaptures.GetMatchupPreviewCapturesQueryHandler>();
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
            services.AddScoped<IGetContestHistoryQueryHandler, GetContestHistoryQueryHandler>();

            // Venues Queries
            services.AddScoped<IGetVenuesQueryHandler, GetVenuesQueryHandler>();
            services.AddScoped<IGetVenueByIdQueryHandler, GetVenueByIdQueryHandler>();

            // Seasons Queries
            services.AddScoped<
                Application.Seasons.Queries.GetCurrentSeason.IGetCurrentSeasonQueryHandler,
                Application.Seasons.Queries.GetCurrentSeason.GetCurrentSeasonQueryHandler>();

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
            services.AddScoped<IInvalidatePickAudits, PickAuditInvalidator>();
            services.AddScoped<IScoreLeagueWeeks, LeagueWeekScoringProcessor>();
            
            // HATEOAS Ref Generator (external API)
            services.AddSingleton<IGenerateApiResourceRefs, ApiResourceRefGenerator>();

            // Athlete Queries
            services.AddScoped<IGetAthleteDetailsQueryHandler, GetAthleteDetailsQueryHandler>();

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
            services.AddScoped<IUpdateUserOptionsCommandHandler, UpdateUserOptionsCommandHandler>();
            services.AddSingleton<IFirebaseUserAdmin, FirebaseUserAdmin>();

            // User Validators
            services.AddValidatorsFromAssemblyContaining<Program>();

            // User Queries
            services.AddScoped<IGetMeQueryHandler, GetMeQueryHandler>();
            services.AddScoped<IGetNotificationPreferencesQueryHandler, GetNotificationPreferencesQueryHandler>();

            // SmackBot Lab composition (docs/features/smackbot-lab.md)
            services.AddScoped<Application.Admin.SmackLab.IGetSmackLabLeaguesQueryHandler, Application.Admin.SmackLab.GetSmackLabLeaguesQueryHandler>();
            services.AddScoped<Application.Admin.SmackLab.IGetSmackLabPicksQueryHandler, Application.Admin.SmackLab.GetSmackLabPicksQueryHandler>();
            services.AddScoped<IGetUserOptionsQueryHandler, GetUserOptionsQueryHandler>();

            services.AddScoped<IUserService, UserService>();
            services.AddScoped<MatchupPreviewGenerator>();
            services.AddScoped<MetricBotWeeklyJob>();
            services.AddScoped<MatchupScheduler>();
            // Scoped: resolves prompts from AppDataContext (text lives in the DB).
            services.AddScoped<IMatchupPreviewPromptProvider, MatchupPreviewPromptProvider>();
            services.AddScoped<Application.Admin.Prompts.ICreatePromptCommandHandler, Application.Admin.Prompts.CreatePromptCommandHandler>();
            services.AddScoped<Application.Admin.Prompts.IImportPromptFromBlobCommandHandler, Application.Admin.Prompts.ImportPromptFromBlobCommandHandler>();
            services.AddScoped<Application.Admin.Prompts.IGetPromptsQueryHandler, Application.Admin.Prompts.GetPromptsQueryHandler>();
            services.AddScoped<Application.Admin.Prompts.IGetPromptByIdQueryHandler, Application.Admin.Prompts.GetPromptByIdQueryHandler>();
            services.AddScoped<Application.Admin.Prompts.IUpdatePromptCommandHandler, Application.Admin.Prompts.UpdatePromptCommandHandler>();
            services.AddScoped<Application.Admin.Prompts.ISetDefaultPromptCommandHandler, Application.Admin.Prompts.SetDefaultPromptCommandHandler>();
            services.AddScoped<Application.Admin.Models.ICreateModelProviderCommandHandler, Application.Admin.Models.CreateModelProviderCommandHandler>();
            services.AddScoped<Application.Admin.Models.IGetModelProvidersQueryHandler, Application.Admin.Models.GetModelProvidersQueryHandler>();
            services.AddScoped<Application.Admin.Models.ICreateModelCommandHandler, Application.Admin.Models.CreateModelCommandHandler>();
            services.AddScoped<Application.Admin.Models.IGetModelsQueryHandler, Application.Admin.Models.GetModelsQueryHandler>();
            services.AddScoped<Application.Admin.Models.IGetModelByIdQueryHandler, Application.Admin.Models.GetModelByIdQueryHandler>();
            services.AddScoped<Application.Admin.Models.IUpdateModelCommandHandler, Application.Admin.Models.UpdateModelCommandHandler>();
            services.AddScoped<Application.Admin.Models.ISetDefaultModelCommandHandler, Application.Admin.Models.SetDefaultModelCommandHandler>();
            services.AddSingleton<GameRecapPromptProvider>();
            services.AddScoped<PickScoringJob>();
            services.AddScoped<LeagueWeekScoringJob>();
            services.AddScoped<LeagueDeactivationJob>();
            services.AddScoped<ILeagueJoinExpiryCalculator, LeagueJoinExpiryCalculator>();
            services.AddScoped<LeagueJoinExpiryAuditJob>();

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

            // Nightly soft-close of finished leagues. Stamps DeactivatedUtc on
            // leagues whose EndsOn is more than 7 days past, dropping them from
            // active surfaces. 4am UTC keeps it clear of the 2/6/9am job cluster.
            recurringJobManager.AddOrUpdate<LeagueDeactivationJob>(
                nameof(LeagueDeactivationJob),
                job => job.ExecuteAsync(),
                Cron.Daily(4));

            // Hourly: drop-week expiries refine from calendar-provisional to
            // first-kickoff-precise as weekly slates land, and this sweep is
            // also the backfill for pre-existing leagues. Cost is a handful
            // of indexed queries per active league.
            recurringJobManager.AddOrUpdate<LeagueJoinExpiryAuditJob>(
                nameof(LeagueJoinExpiryAuditJob),
                job => job.ExecuteAsync(),
                Cron.Hourly());

            recurringJobManager.AddOrUpdate<MatchupPreviewGenerator>(
                nameof(MatchupPreviewGenerator),
                job => job.ExecuteAsync(),
                Cron.Weekly);

            // deetsMeter predictions via the MetricBot service. Scheduled
            // ahead of each league's slate lock: NCAAFB games start
            // Thursday, NFL on Thursday too but its week rolls later, so
            // NCAA runs Tuesday 03:00 UTC and NFL Wednesday 03:00 UTC.
            // Hangfire (not a K8s CronJob) so the dashboard's manual
            // trigger covers ad-hoc reruns; parameterized experiment runs
            // go through POST /admin/metricbot/run-week instead.
            recurringJobManager.AddOrUpdate<MetricBotWeeklyJob>(
                "MetricBotWeekly-FootballNcaa",
                job => job.ExecuteAsync(Sport.FootballNcaa),
                "0 3 * * 2");

            recurringJobManager.AddOrUpdate<MetricBotWeeklyJob>(
                "MetricBotWeekly-FootballNfl",
                job => job.ExecuteAsync(Sport.FootballNfl),
                "0 3 * * 3");

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
            // unfinalized contest.
            //
            // Timing is pinned between two constraints:
            //   - AFTER games finalize. Contests finalize and enrich roughly
            //     00:00–06:00 UTC (evening ET through the latest West Coast
            //     finishes). The old 02:00–02:30 slot sat inside that window,
            //     so the audit both competed with live Producer traffic and
            //     widened the race where a correction landing mid-audit is
            //     watermarked against a stale result (see the note at the
            //     stamp site in PickScoringAuditProcessor).
            //   - BEFORE PickScoringJob at 09:00, because the audit resets
            //     ScoredAt on picks whose contest turns out not to be
            //     finalized, and that daily rescore is what picks them back
            //     up. Running after it would delay every reset a full day.
            // 08:00 clears the latest finishes with well over an hour of
            // buffer and still leaves an hour before the rescore — ample,
            // since the watermark keeps a typical night's work near zero.
            //
            // Stagger by 15 min per sport so a single sport's audit owns
            // its window in Seq — pods are separate so the lack of stagger
            // wouldn't actually collide, but staggering makes "which sport
            // is misbehaving" trivial to identify.
            recurringJobManager.AddOrUpdate<PickScoringAuditJob>(
                "PickScoringAudit-FootballNcaa",
                job => job.ExecuteAsync(Sport.FootballNcaa),
                Cron.Daily(8));

            recurringJobManager.AddOrUpdate<PickScoringAuditJob>(
                "PickScoringAudit-FootballNfl",
                job => job.ExecuteAsync(Sport.FootballNfl),
                Cron.Daily(8, 15));

            recurringJobManager.AddOrUpdate<PickScoringAuditJob>(
                "PickScoringAudit-BaseballMlb",
                job => job.ExecuteAsync(Sport.BaseballMlb),
                Cron.Daily(8, 30));

            return services;
        }
    }
}
