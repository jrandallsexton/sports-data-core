using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Consumers;
using SportsData.Producer.Application.Documents;
using SportsData.Producer.Application.Images.Handlers;
using SportsData.Producer.Config;
using SportsData.Producer.DependencyInjection;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Golf;
using SportsData.Producer.Mapping;

using System.Reflection;

namespace SportsData.Producer;

public class Program
{
    public static async Task Main(string[] args)
    {
        var mode = CommandLineHelpers.ParseFlag<Sport>(args, "-mode", Sport.All);
        var role = CommandLineHelpers.ParseFlag<ProducerRole>(args, "-role", ProducerRole.All);

        Console.WriteLine($"Mode: {mode}");
        Console.WriteLine($"Role: {role}");

        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        var config = builder.Configuration;
        config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName, mode);

        builder.UseCommon();

        var services = builder.Services;
        services.AddCoreServices(config, mode);

        // API controllers — only for Api role
        if (role.HasFlag(ProducerRole.Api))
        {
            services.AddControllers();
        }

        services.AddEndpointsApiExplorer();

        // Swagger services registered for all roles — UseCommonFeatures adds
        // Swagger middleware unconditionally so the DI container must have
        // ISwaggerProvider even on Worker/Ingest pods (it's a no-op without controllers)
        services.AddSwaggerGen();

        services.AddInstrumentation(builder.Environment.ApplicationName, config);

        services.AddClients(config);

        // Per-role connection pool sizing — configurable via Azure App Config.
        // Keys: {appName}:ConnectionPool:Worker, :Api, :Ingest
        // Defaults: Worker=22, Api=5, Ingest=5
        var (roleName, defaultPoolSize) = role switch
        {
            _ when role.HasFlag(ProducerRole.Api) && !role.HasFlag(ProducerRole.Worker) => ("Api", 5),
            _ when role.HasFlag(ProducerRole.Ingest) && !role.HasFlag(ProducerRole.Worker) => ("Ingest", 5),
            _ when role.HasFlag(ProducerRole.Worker) => ("Worker", 22),
            _ => ("Default", 22)
        };
        int? maxPoolSize = Core.DependencyInjection.ServiceRegistration.ResolvePoolSize(config, builder.Environment.ApplicationName, roleName, defaultPoolSize);
        Console.WriteLine($"Role: {roleName}, ConnectionPool MaxSize: {maxPoolSize}");

        switch (mode)
        {
            case Sport.GolfPga:
                services.AddDataPersistence<GolfDataContext>(config, builder.Environment.ApplicationName, mode, maxPoolSize);
                break;
            case Sport.FootballNcaa:
            case Sport.FootballNfl:
                services.AddDataPersistence<FootballDataContext>(config, builder.Environment.ApplicationName, mode, maxPoolSize);

                // Abstract type registrations needed for services that inject them directly
                // Note: These are NOT used by document processors (factories inject FootballDataContext)
                // but other services (ContestEnrichmentJob, FranchiseSeasonEnrichmentProcessor, etc.) still need them
                services.AddScoped<TeamSportDataContext, FootballDataContext>();
                services.AddScoped<BaseDataContext, FootballDataContext>();
                break;
            case Sport.All:
            case Sport.BaseballMlb:
            case Sport.BasketballNba:
            default:
                throw new ArgumentOutOfRangeException();
        }

        // Hangfire — Worker gets client + server; Ingest and Api get client only
        // Api needs client so controllers can enqueue jobs; Ingest needs it to enqueue from MassTransit consumers
        var needsHangfireServer = role.HasFlag(ProducerRole.Worker);
        services.AddHangfire(config, builder.Environment.ApplicationName, mode,
            includeServer: needsHangfireServer, maxPoolSize: maxPoolSize);

        // MassTransit consumers — only for Ingest role
        if (role.HasFlag(ProducerRole.Ingest))
        {
            var consumers = new List<Type>
            {
                typeof(DocumentCreatedHandler),
                // typeof(DocumentDeadLetterConsumer), // DISABLED: Allow messages to accumulate for later replay
                typeof(LoadTestProducerEventConsumer),
                typeof(ProcessImageRequestedHandler),
                typeof(ProcessImageResponseHandler)
            };

            switch (mode)
            {
                case Sport.FootballNcaa:
                case Sport.FootballNfl:
                    services.AddMessaging<FootballDataContext>(config, consumers);
                    break;
                case Sport.GolfPga:
                    services.AddMessaging<GolfDataContext>(config, consumers);
                    break;
                case Sport.All:
                case Sport.BaseballMlb:
                case Sport.BasketballNba:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        else
        {
            // Non-ingest roles still need MassTransit bus for publishing events
            // Must use generic overload to preserve EF outbox pattern
            switch (mode)
            {
                case Sport.FootballNcaa:
                case Sport.FootballNfl:
                    services.AddMessaging<FootballDataContext>(config, consumers: null);
                    break;
                case Sport.GolfPga:
                    services.AddMessaging<GolfDataContext>(config, consumers: null);
                    break;
                case Sport.All:
                case Sport.BaseballMlb:
                case Sport.BasketballNba:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // Ensure required queues (e.g., document-dead-letter) exist in RabbitMQ at startup,
        // even when the consumer is disabled for accumulation/replay.
        services.AddHostedService<SportsData.Core.Infrastructure.Messaging.EnsureQueuesHostedService>();

        switch (mode)
        {
            case Sport.GolfPga:
                services.AddHealthChecks<GolfDataContext, Program>(builder.Environment.ApplicationName, mode);
                break;
            case Sport.FootballNcaa:
            case Sport.FootballNfl:
                services.AddHealthChecks<FootballDataContext, Program>(builder.Environment.ApplicationName, mode);
                break;
            case Sport.All:
            case Sport.BaseballMlb:
            case Sport.BasketballNba:
            default:
                throw new ArgumentOutOfRangeException();
        }

        services.AddLocalServices(mode);

        var hostAssembly = Assembly.GetExecutingAssembly();
        services.AddAutoMapper(typeof(MappingProfile));
        services.AddMediatR(hostAssembly);

        var app = builder.Build();

        // Don't redirect to HTTPS when behind a proxy (Front Door, Traefik)
        // app.UseHttpsRedirection();

        using (var scope = app.Services.CreateScope())
        {
            var appServices = scope.ServiceProvider;

            switch (mode)
            {
                case Sport.GolfPga:
                    var golfContext = appServices.GetRequiredService<GolfDataContext>();
                    await golfContext.Database.MigrateAsync();
                    break;
                case Sport.FootballNcaa:
                case Sport.FootballNfl:
                    var context = appServices.GetRequiredService<FootballDataContext>();
                    await context.Database.MigrateAsync();
                    break;
                case Sport.All:
                case Sport.BaseballMlb:
                case Sport.BasketballNba:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        app.UseCommonFeatures();

        if (role.HasFlag(ProducerRole.Api))
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
        if (role.HasFlag(ProducerRole.Worker))
        {
            app.Services.ConfigureHangfireJobs(mode);
        }

        await app.RunAsync();
    }

}
