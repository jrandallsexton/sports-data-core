using Hangfire;
using Hangfire.Dashboard;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Consumers;
using SportsData.Producer.Application.Documents;
using SportsData.Producer.Application.Events;
using SportsData.Producer.Application.Images.Handlers;
using SportsData.Producer.Config;
using SportsData.Producer.DependencyInjection;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
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
        // Note: AddCommonConfiguration handles the post-AppConfig env-var
        // re-add for non-Production environments (gated inside the helper),
        // so Provider/Producer/Api all share the same precedence wiring.

        builder.WithLoggingContext(mode, role.ToString());
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
        // Keys: {appName}:ConnectionPool:All, :Worker, :Api, :Ingest, :Daemon
        // Defaults: All=60, Worker=22, Daemon=10, Api=5, Ingest=5
        //
        // The All arm covers the local-dev / docker-compose case where one
        // process runs every role concurrently and starves the Worker-sized
        // pool. In prod K8s each role is its own pod, so this branch never
        // matches there — strictly additive.
        //
        // Daemon pods host long-running streamer jobs that spend most of their
        // time sleeping between ESPN polls; default pool of 10 covers ~10-15
        // concurrent streamers per pod comfortably.
        // Worker and Daemon are mutually-exclusive host-pod roles in production
        // (separate K8s Deployments). Order matters here: Worker is checked before
        // Daemon so an accidental `Worker|Daemon` combo gets Worker's larger pool
        // and both-queue listening — safer fallback than the smaller Daemon pool.
        var (roleName, defaultPoolSize) = role switch
        {
            _ when role == ProducerRole.All => ("All", 60),
            _ when role.HasFlag(ProducerRole.Api) && !role.HasFlag(ProducerRole.Worker) && !role.HasFlag(ProducerRole.Daemon) => ("Api", 5),
            _ when role.HasFlag(ProducerRole.Ingest) && !role.HasFlag(ProducerRole.Worker) && !role.HasFlag(ProducerRole.Daemon) => ("Ingest", 5),
            _ when role.HasFlag(ProducerRole.Worker) => ("Worker", 22),
            _ when role.HasFlag(ProducerRole.Daemon) => ("Daemon", 10),
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
            case Sport.BaseballMlb:
                services.AddDataPersistence<BaseballDataContext>(config, builder.Environment.ApplicationName, mode, maxPoolSize);
                services.AddScoped<TeamSportDataContext, BaseballDataContext>();
                services.AddScoped<BaseDataContext, BaseballDataContext>();
                break;
            case Sport.All:
            case Sport.BasketballNba:
            default:
                throw new ArgumentOutOfRangeException();
        }

        // Hangfire — Worker and Daemon get client + server; Ingest and Api get client only.
        // Api needs client so controllers can enqueue jobs; Ingest needs it to enqueue from
        // MassTransit consumers. Daemon hosts long-running streamer jobs on its own queue.
        //
        // Queue routing:
        //   - Daemon-only pods listen on ["daemon"] exclusively.
        //   - Worker pods listen on ["default", "daemon"] during the streamer-cutover
        //     transition (PR A–C). PR D will drop "daemon" from Worker once Daemon pods
        //     are confirmed healthy in prod.
        //   - All (local-dev / docker-compose) listens on both.
        //   - Other combos fall back to Hangfire's default ["default"].
        // See docs/contest-finalization-reconcile-backstop.md Step 4.
        var needsHangfireServer = role.HasFlag(ProducerRole.Worker) || role.HasFlag(ProducerRole.Daemon);
        // Worker checked before Daemon (same mutual-exclusion rationale as the
        // pool-sizing switch above): an accidental `Worker|Daemon` combo falls
        // through to the both-queues case, which is the safer fallback.
        string[]? hangfireQueues = role switch
        {
            _ when role == ProducerRole.All => new[] { "default", "daemon" },
            _ when role.HasFlag(ProducerRole.Worker) => new[] { "default", "daemon" },
            _ when role.HasFlag(ProducerRole.Daemon) => new[] { "daemon" },
            _ => null
        };
        services.AddHangfire(config, builder.Environment.ApplicationName, mode,
            includeServer: needsHangfireServer, maxPoolSize: maxPoolSize, queues: hangfireQueues);

        // MassTransit consumers — only for Ingest role
        if (role.HasFlag(ProducerRole.Ingest))
        {
            var consumers = new List<Type>
            {
                typeof(CompetitorScoreUpdatedConsumer),
                typeof(ContestCompletedHandler),
                typeof(ContestStartTimeUpdatedConsumer),
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
                case Sport.BaseballMlb:
                    services.AddMessaging<BaseballDataContext>(config, consumers);
                    break;
                case Sport.All:
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
                case Sport.BaseballMlb:
                    services.AddMessaging<BaseballDataContext>(config, consumers: null);
                    break;
                case Sport.All:
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
                services.AddHealthChecks<GolfDataContext>(builder.Environment.ApplicationName, mode);
                break;
            case Sport.FootballNcaa:
            case Sport.FootballNfl:
                services.AddHealthChecks<FootballDataContext>(builder.Environment.ApplicationName, mode);
                break;
            case Sport.BaseballMlb:
                services.AddHealthChecks<BaseballDataContext>(builder.Environment.ApplicationName, mode);
                break;
            case Sport.All:
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
                case Sport.BaseballMlb:
                    var baseballContext = appServices.GetRequiredService<BaseballDataContext>();
                    await baseballContext.Database.MigrateAsync();
                    break;
                case Sport.All:
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

            // Hangfire dashboard for the Development environment only.
            // Tightened from !IsProduction() so Staging/QA don't inadvertently
            // expose the dashboard. Prod cluster aggregates dashboards via
            // SportsData.JobsDashboard at jobs.sportdeets.com behind basic
            // auth; per-pod /dashboard would just be redundant surface area.
            //
            // The empty authorization filter array is intentional — Hangfire's
            // default LocalRequestsOnlyAuthorizationFilter rejects requests
            // from outside the container, which blocks docker-compose access
            // from the host machine (the container sees host requests as
            // remote). Safe in Development because the container's port is
            // only bound to localhost via docker-compose `ports:` mapping.
            if (app.Environment.IsDevelopment())
            {
                app.UseHangfireDashboard("/dashboard", new DashboardOptions
                {
                    Authorization = Array.Empty<IDashboardAuthorizationFilter>()
                });
            }
        }

        // Map Prometheus metrics endpoint only if OpenTelemetry metrics are enabled
        var otelConfig = config.GetSection("CommonConfig:OpenTelemetry").Get<SportsData.Core.Config.OpenTelemetryConfig>();
        if (otelConfig?.Enabled == true && otelConfig.Metrics?.Enabled == true)
        {
            app.MapPrometheusScrapingEndpoint();
        }

        // Recurring Hangfire jobs — for Worker and Daemon roles. Hangfire's
        // AddOrUpdate is idempotent so duplicate registration across roles is
        // safe; per-job [Queue(...)] attributes route each trigger to the
        // appropriate role's queue at run time (e.g. FinalizationReconcileJob
        // is [Queue("daemon")] so only Daemon pods process it).
        if (role.HasFlag(ProducerRole.Worker) || role.HasFlag(ProducerRole.Daemon))
        {
            app.Services.ConfigureHangfireJobs(mode);
        }

        await app.RunAsync();
    }

}
