using FirebaseAdmin;

using Google.Apis.Auth.OAuth2;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Application.Scheduling;
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
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName, mode);

            services.AddMessaging(config, [
                typeof(ContestOddsUpdatedConsumer),
                typeof(ContestStartTimeUpdatedConsumer),
                typeof(PickemGroupCreatedConsumer),
                typeof(PickemGroupDataPublishedConsumer),
                typeof(PickemGroupMatchupCreatedConsumer),
                typeof(PickemGroupMatchupDataPublishedConsumer),
                typeof(PickemGroupMemberAddedConsumer),
                typeof(UserDataPublishedConsumer),
                typeof(UserDeviceRegisteredConsumer),
                typeof(UserDeviceUnregisteredConsumer),
                typeof(UserInvitedToPickemGroupConsumer),
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
            services.AddHangfire(config, builder.Environment.ApplicationName, mode);
            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();

            // Phase 2c-main: pick-deadline reminder scheduling + dispatch.
            // Phase 2d: contest-start reminder scheduling — same dispatcher,
            // per-contest scope, sport-aware copy at fire time. Dispatcher is
            // the Hangfire-invoked target; each scheduler is the helper
            // consumers call after a projection write that could affect its
            // respective scope.
            services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
            services.AddScoped<IPickDeadlineReminderScheduler, PickDeadlineReminderScheduler>();
            services.AddScoped<IContestStartReminderScheduler, ContestStartReminderScheduler>();

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
