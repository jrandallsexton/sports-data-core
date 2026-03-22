using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Config;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Consumers;
using SportsData.Provider.Application.Documents;
using SportsData.Provider.Config;
using SportsData.Provider.DependencyInjection;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Data.Seeders;
using SportsData.Provider.Middleware.Health;

namespace SportsData.Provider
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var mode = ParseFlag<Sport>(args, "-mode", Sport.All);
            var role = ParseFlag<ProviderRole>(args, "-role", ProviderRole.All);

            Console.WriteLine($"Mode: {mode}");
            Console.WriteLine($"Role: {role}");

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var config = builder.Configuration;
            config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName, mode);

            builder.UseCommon();

            var services = builder.Services;
            services.AddCoreServices(config, mode);
            services.AddEndpointsApiExplorer();

            // API controllers — only for Api role
            if (role.HasFlag(ProviderRole.Api))
            {
                services.AddControllers();
                services.AddSwaggerGen();
            }

            services.AddClients(config);
            services.AddCaching(config);

            // ESPN circuit breaker and rate limiter — needed by Worker (Hangfire jobs) and Ingest
            // (resource index pagination in DocumentRequestedHandler)
            if (role.HasFlag(ProviderRole.Worker) || role.HasFlag(ProviderRole.Ingest))
            {
                if (!string.IsNullOrWhiteSpace(config[CommonConfigKeys.CacheServiceUri]))
                {
                    services.AddSingleton<IEspnCircuitBreaker, RedisEspnCircuitBreaker>();
                    services.AddSingleton<IEspnRateLimiter, RedisEspnRateLimiter>();
                }
            }

            // Api and Ingest roles barely touch PostgreSQL — use smaller connection pools
            // to stay well under PostgreSQL's 500 max_connections limit
            int? maxPoolSize = role switch
            {
                _ when role == ProviderRole.Api => 5,
                _ when role == ProviderRole.Ingest => 5,
                _ => null // Worker and All use the default from the connection string
            };
            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName, mode, maxPoolSize);

            // Hangfire — Worker gets client + server; Ingest and Api get client only
            // Api needs client so controllers can enqueue jobs; Ingest needs it to enqueue from MassTransit consumers
            var needsHangfireServer = role.HasFlag(ProviderRole.Worker);
            services.AddHangfire(config, builder.Environment.ApplicationName, mode,
                includeServer: needsHangfireServer, maxPoolSize: maxPoolSize);

            // MassTransit consumers — only for Ingest role
            if (role.HasFlag(ProviderRole.Ingest))
            {
                var workerConfig = config.GetSection($"{builder.Environment.ApplicationName}:WorkerConfig")
                    .Get<ProviderWorkerConfig>();

                var consumers = new List<Type>
                {
                    typeof(LoadTestProviderEventConsumer),
                    typeof(TriggerTierSourcingConsumer)
                };

                if (workerConfig?.PauseMessageConsumption == true)
                {
                    Console.WriteLine("Message consumption is PAUSED. DocumentRequestedHandler will not be registered.");
                }
                else
                {
                    consumers.Add(typeof(DocumentRequestedHandler));
                }

                services.AddMessaging(config, consumers, busConfig =>
                {
                    busConfig.AddSagaSupport();
                });
            }
            else
            {
                // Non-ingest roles still need MassTransit bus for publishing events
                services.AddMessaging(config, consumers: null);
            }

            services.AddInstrumentation(builder.Environment.ApplicationName, config);

            builder.Services.Configure<ProviderDocDatabaseConfig>(
                builder.Configuration.GetSection($"{builder.Environment.ApplicationName}:ProviderDocDatabaseConfig"));

            services.AddHealthChecks<AppDataContext, Program>(builder.Environment.ApplicationName, mode);
            services.AddHealthChecks().AddCheck<DocumentDatabaseHealthCheck>(nameof(DocumentDatabaseHealthCheck));

            var docDbProviderValue = config["SportsData.Provider:ProviderDocDatabaseConfig:Provider"];
            var useMongo = docDbProviderValue == "Mongo";
            services.AddLocalServices(builder.Configuration, mode, useMongo);

            var app = builder.Build();

            // Don't redirect to HTTPS when behind a proxy (Front Door, Traefik)
            // app.UseHttpsRedirection();

            // Apply migrations and seed data once using the real provider
            await app.Services.ApplyMigrations<AppDataContext>(ctx => LoadSeedData(ctx, mode));

            app.UseCommonFeatures();

            if (role.HasFlag(ProviderRole.Api))
            {
                app.UseAuthorization();
                app.MapControllers();
            }

            // Map Prometheus metrics endpoint only if OpenTelemetry metrics are enabled
            var otelConfig = config.GetSection("CommonConfig:OpenTelemetry").Get<SportsData.Core.Config.OpenTelemetryConfig>();
            if (otelConfig?.Enabled == true && otelConfig.Metrics?.Enabled == true)
            {
                app.MapPrometheusScrapingEndpoint();
            }

            // Recurring Hangfire jobs — only for Worker role
            if (role.HasFlag(ProviderRole.Worker))
            {
                app.Services.ConfigureHangfireJobs(mode);
            }

            await app.RunAsync();
        }

        private static T ParseFlag<T>(string[] args, string flag, T defaultValue) where T : struct, Enum
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == flag)
                {
                    if (!Enum.TryParse<T>(args[i + 1], ignoreCase: true, out var value))
                    {
                        var valid = string.Join(", ", Enum.GetNames<T>());
                        throw new ArgumentException(
                            $"Invalid value '{args[i + 1]}' for {flag}. Valid values: {valid}");
                    }
                    return value;
                }
            }

            return defaultValue;
        }

        private static async Task LoadSeedData(AppDataContext dbContext, Sport mode)
        {
            if (await dbContext.ResourceIndexJobs.AnyAsync())
                return;

            switch (mode)
            {
                case Sport.FootballNcaa:
                case Sport.FootballNfl:
                    var footballValues = new FootballSeeder().Generate(mode, [2025]);
                    await dbContext.ResourceIndexJobs.AddRangeAsync(footballValues);
                    await dbContext.SaveChangesAsync();
                    break;
                case Sport.GolfPga:
                    var golfValues = new GolfSeeder().Generate(mode, [2025]);
                    await dbContext.ResourceIndexJobs.AddRangeAsync(golfValues);
                    await dbContext.SaveChangesAsync();
                    break;
                case Sport.BasketballNba:
                    var basketballValues = new BasketballSeeder().Generate(mode, [2025]);
                    await dbContext.ResourceIndexJobs.AddRangeAsync(basketballValues);
                    await dbContext.SaveChangesAsync();
                    break;
                case Sport.BaseballMlb:
                    break;
                case Sport.All:
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
    }
}
