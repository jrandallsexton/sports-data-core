using FirebaseAdmin;

using Google.Apis.Auth.OAuth2;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Notification.Application.Consumers;
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
                typeof(ContestStartTimeUpdatedConsumer),
                typeof(PickemGroupCreatedConsumer),
                typeof(PickemGroupMatchupCreatedConsumer),
                typeof(PickemGroupMemberAddedConsumer),
                typeof(UserPickScoredConsumer)
            ]);

            // Initialize FirebaseApp.DefaultInstance from CommonConfig:Firebase.
            // FirebasePushNotificationSender depends on the global default
            // instance — same pattern as API's Program.cs (single source of
            // truth in CommonConfig means API + Notification point at the
            // same Firebase project). Skipped if the config section is empty
            // so local-dev / docker-compose pods boot without credentials.
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
            }

            services.AddScoped<IPushNotificationSender, FirebasePushNotificationSender>();

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
