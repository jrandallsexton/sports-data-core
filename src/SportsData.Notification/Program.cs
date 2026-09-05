using FirebaseAdmin;

using Google.Apis.Auth.OAuth2;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Notification.Application.Backfill.Commands.RequestPickemGroupMatchupsBackfill;
using SportsData.Notification.Application.Backfill.Commands.RequestPickemGroupsBackfill;
using SportsData.Notification.Application.Backfill.Commands.RequestUsersBackfill;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Application.Reminders;
using SportsData.Notification.Application.Reminders.Commands.SendContestStartReminder;
using SportsData.Notification.Application.Reminders.Commands.SendPickDeadlineReminder;
using SportsData.Notification.Application.Scheduling;
using SportsData.Notification.Application.Smack.Commands.CreateSmackPhrase;
using SportsData.Notification.Application.Smack.Commands.RateSmackPreview;
using SportsData.Notification.Application.Smack.Commands.UpdateSmackPhrase;
using SportsData.Notification.Application.Smack.Queries.GetSmackPhrases;
using SportsData.Notification.Application.Smack.Queries.GetSmackRatings;
using SportsData.Notification.Application.Smack.Queries.PreviewSmack;
using SportsData.Notification.Config;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Notifications;

namespace SportsData.Notification
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var mode = CommandLineHelpers.ParseFlag<Sport>(args, "-mode", Sport.All);

            Console.WriteLine($"Mode: {mode}");

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var config = builder.Configuration;
            config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName, mode);

            builder.UseCommon();

            var services = builder.Services;
            services.AddCoreServices(config, mode);
            // Typed service clients. ContestOddsUpdatedConsumer resolves
            // IContestClientFactory to enrich line-move pushes with the
            // matchup (Producer owns canonical contest data). Every client
            // call there degrades gracefully, so a missing
            // CommonConfig:ContestClientConfig:* slot costs the enrichment,
            // not the notification.
            services.AddClients(config, mode);
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            // Clamp the data + Hangfire pools — default leaves Npgsql at 100 each
            // (200/pod). See docs/infrastructure/postgres-connection-budget.md.
            var dataPoolSize = Core.DependencyInjection.ServiceRegistration.ResolvePoolSize(
                config, builder.Environment.ApplicationName, "AppData", defaultPoolSize: 15);
            var hangfirePoolSize = Core.DependencyInjection.ServiceRegistration.ResolvePoolSize(
                config, builder.Environment.ApplicationName, "Hangfire", defaultPoolSize: 15);
            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName, mode, dataPoolSize);

            services.AddMessaging(config, [
                typeof(ContestOddsUpdatedConsumer),
                typeof(ContestStartTimeUpdatedConsumer),
                typeof(PickemGroupCreatedConsumer),
                typeof(PickemGroupDataPublishedConsumer),
                typeof(PickemGroupMatchupCreatedConsumer),
                typeof(PickemGroupMatchupDataPublishedConsumer),
                typeof(PickemGroupMemberAddedConsumer),
                typeof(UserDataPublishedConsumer),
                typeof(UserDeletedConsumer),
                typeof(UserDeviceRegisteredConsumer),
                typeof(UserDeviceUnregisteredConsumer),
                typeof(UserInvitedToPickemGroupConsumer),
                typeof(UserNotificationPreferencesUpdatedConsumer),
                typeof(UserPickMadeConsumer),
                typeof(UserPickScoredConsumer)
            ]);

            // Initialize FirebaseApp.DefaultInstance from CommonConfig:Firebase
            // and register the real sender. When ProjectId is empty (local
            // dev / tests without Firebase credentials) we register a no-op
            // sender instead so consumers don't crash on resolution. The
            // no-op returns Failure with an explicit "not configured" reason,
            // which lands in NotificationLog as Failed_FcmError — easy to
            // grep, makes the misconfiguration obvious without flooding
            // dead-letter.
            var firebaseSection = config.GetSection("CommonConfig:Firebase");
            if (!string.IsNullOrWhiteSpace(firebaseSection["ProjectId"]))
            {
                var firebaseJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    type = firebaseSection["Type"],
                    project_id = firebaseSection["ProjectId"],
                    private_key_id = firebaseSection["PrivateKeyId"],
                    private_key = firebaseSection["PrivateKey"],
                    client_email = firebaseSection["ClientEmail"],
                    client_id = firebaseSection["ClientId"],
                    auth_uri = firebaseSection["AuthUri"],
                    token_uri = firebaseSection["TokenUri"],
                    auth_provider_x509_cert_url = firebaseSection["AuthProviderX509CertUrl"],
                    client_x509_cert_url = firebaseSection["ClientX509CertUrl"],
                    universe_domain = firebaseSection["UniverseDomain"]
                });

                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromJson(firebaseJson)
                });

                services.AddScoped<IPushNotificationSender, FirebasePushNotificationSender>();
            }
            else
            {
                Console.WriteLine("WARN: CommonConfig:Firebase:ProjectId is not set; registering NoOpPushNotificationSender. FCM dispatches will no-op.");
                services.AddScoped<IPushNotificationSender, NoOpPushNotificationSender>();
            }

            // Hangfire — Notification hosts BOTH client (consumers schedule
            // reminder dispatches) and server (those scheduled jobs run in-pod).
            // Storage lives in its own database sdNotification.{mode}.Hangfire
            // per the established AddHangfire helper convention; the DB must be
            // pre-created (see PR description for the one-time provisioning
            // step). No dashboard mounted — production dashboards aggregate
            // via SportsData.JobsDashboard at jobs.sportdeets.com behind basic
            // auth, per the convention reasserted in #463.
            services.AddHangfire(config, builder.Environment.ApplicationName, mode, maxPoolSize: hangfirePoolSize);
            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();

            // Reminder knobs (lead/coalesce minutes) — section may be absent
            // locally; class defaults apply. Same binding shape as API's
            // SportsData.Api:ApiConfig.
            services.Configure<NotificationConfig>(config.GetSection("SportsData.Notification:NotificationConfig"));

            // Reminder dispatch is vertically sliced (Application/Reminders):
            // each reminder's Hangfire-invoked handler owns its claim +
            // gates + copy; StaleFireGuard and PushDeviceFanout are the
            // shared pieces both slices use. Each scheduler is the helper
            // consumers call after a projection write that could affect its
            // respective scope. The old NotificationDispatcher was deleted
            // with the refactor — its in-flight Hangfire jobs are handled by
            // the deploy runbook (bulk-delete Scheduled jobs, then backfill
            // rebuilds every reminder against the slice handlers; see
            // docs/features/pick-deadline-reminders-v2.md).
            services.AddScoped<IStaleFireGuard, StaleFireGuard>();
            services.AddScoped<IPushDeviceFanout, PushDeviceFanout>();
            services.AddScoped<ISendPickDeadlineReminderCommandHandler, SendPickDeadlineReminderCommandHandler>();
            services.AddScoped<ISendContestStartReminderCommandHandler, SendContestStartReminderCommandHandler>();
            services.AddScoped<ISmackPhraseCatalog, SmackPhraseCatalog>();
            services.AddScoped<IPickDeadlineReminderScheduler, PickDeadlineReminderScheduler>();
            services.AddScoped<IContestStartReminderScheduler, ContestStartReminderScheduler>();

            // Vertical-slice handlers (Application/{Feature}/{Queries|Commands}),
            // resolved per-action via [FromServices] — no MediatR by design.
            services.AddScoped<IPreviewSmackQueryHandler, PreviewSmackQueryHandler>();
            services.AddScoped<IGetSmackPhrasesQueryHandler, GetSmackPhrasesQueryHandler>();
            services.AddScoped<IGetSmackRatingsQueryHandler, GetSmackRatingsQueryHandler>();
            services.AddScoped<ICreateSmackPhraseCommandHandler, CreateSmackPhraseCommandHandler>();
            services.AddScoped<IUpdateSmackPhraseCommandHandler, UpdateSmackPhraseCommandHandler>();
            services.AddScoped<IRateSmackPreviewCommandHandler, RateSmackPreviewCommandHandler>();
            services.AddScoped<IRequestUsersBackfillCommandHandler, RequestUsersBackfillCommandHandler>();
            services.AddScoped<IRequestPickemGroupsBackfillCommandHandler, RequestPickemGroupsBackfillCommandHandler>();
            services.AddScoped<IRequestPickemGroupMatchupsBackfillCommandHandler, RequestPickemGroupMatchupsBackfillCommandHandler>();

            services.AddInstrumentation(builder.Environment.ApplicationName, config);
            services.AddHealthChecks<AppDataContext>(builder.Environment.ApplicationName, mode);

            var app = builder.Build();

            await app.Services.ApplyMigrations<AppDataContext>();

            // Configure the HTTP request pipeline.
            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseCommonFeatures();

            app.MapControllers();

            await app.RunAsync();
        }
    }
}
