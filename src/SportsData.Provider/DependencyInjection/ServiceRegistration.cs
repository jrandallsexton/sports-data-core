using Hangfire;

using MassTransit;

using Polly;

using SportsData.Core.Common;
using SportsData.Core.Common.Parsing;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Application.Services;
using SportsData.Provider.Application.Sourcing.Historical;
using SportsData.Provider.Application.Sourcing.Historical.Saga;
using SportsData.Provider.Infrastructure.Data;

using System.Net;
using System.Net.Security;
using System.Security.Authentication;

namespace SportsData.Provider.DependencyInjection
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddLocalServices(
            this IServiceCollection services,
            ConfigurationManager configuration,
            Sport mode,
            bool useMongo)
        {
            services.AddDataPersistenceExternal();

            services.AddScoped<IProcessResourceIndexes, ResourceIndexJob>();
            services.AddScoped<IProcessResourceIndexItems, ResourceIndexItemProcessor>();
            services.AddScoped<IResourceIndexItemParser, ResourceIndexItemParser>();
            services.AddScoped<IProvideBackgroundJobs, BackgroundJobProvider>();
            services.AddScoped<IDocumentInclusionService, DocumentInclusionService>();

            // Historical sourcing services
            services.AddOptions<HistoricalSourcingConfig>()
                .Bind(configuration.GetSection(HistoricalSourcingConfig.SectionName))
                .Validate(config =>
                {
                    // Validate SagaConfig properties
                    if (config.SagaConfig.CompletionThreshold <= 0)
                        return false;
                    if (config.SagaConfig.FlagPercentage < 0 || config.SagaConfig.FlagPercentage > 1)
                        return false;
                    if (config.SagaConfig.MinimumFlaggedDocuments < 0)
                        return false;
                    if (config.SagaConfig.AlertAfterMinutes <= 0)
                        return false;
                    return true;
                },
                "HistoricalSourcingConfig validation failed: " +
                "CompletionThreshold must be > 0, " +
                "FlagPercentage must be between 0 and 1 (inclusive), " +
                "MinimumFlaggedDocuments must be >= 0, " +
                "AlertAfterMinutes must be > 0.");
            services.AddScoped<IHistoricalSourcingUriBuilder, HistoricalSourcingUriBuilder>();
            services.AddScoped<IHistoricalSeasonSourcingService, HistoricalSeasonSourcingService>();

            var imageClient = services.AddHttpClient("images", c =>
            {
                c.Timeout = TimeSpan.FromSeconds(15);
                c.DefaultRequestVersion = HttpVersion.Version20;
                c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
                c.DefaultRequestHeaders.UserAgent.ParseAdd("SportDeets-Provider/1.0");
                c.DefaultRequestHeaders.Accept.ParseAdd("image/*");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 64,
                AutomaticDecompression = DecompressionMethods.All,
                SslOptions = new SslClientAuthenticationOptions
                {
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }
            });
            imageClient.AddPolicyHandler(Policy<HttpResponseMessage>
                .Handle<HttpRequestException>().Or<IOException>()
                .OrResult(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout)
                .WaitAndRetryAsync(3, i => TimeSpan.FromMilliseconds(200 * Math.Pow(2, i))));

            services.AddScoped<IProcessPublishDocumentEvents, PublishDocumentEventsProcessor>();

            if (useMongo)
            {
                services.AddSingleton<IDocumentStore, MongoDocumentService>();
            }
            else
            {
                services.AddSingleton<IDocumentStore, CosmosDocumentService>();
            }

            return services;
        }

        public static IBusRegistrationConfigurator AddSagaSupport(
            this IBusRegistrationConfigurator busConfigurator)
        {
            // Register the saga state machine
            // Optimistic concurrency is handled via RowVersion property configured in HistoricalSeasonSourcingStateConfiguration
            busConfigurator.AddSagaStateMachine<HistoricalSeasonSourcingSaga, HistoricalSeasonSourcingState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<AppDataContext>();
                    r.UsePostgres();
                });

            return busConfigurator;
        }

        public static IServiceProvider ConfigureHangfireJobs(
            this IServiceProvider services,
            Sport mode)
        {
            var serviceScope = services.CreateScope();

            var recurringJobManager = serviceScope.ServiceProvider
                .GetRequiredService<IRecurringJobManager>();

            recurringJobManager.AddOrUpdate<SourcingJobOrchestrator>(
                nameof(SourcingJobOrchestrator),
                job => job.ExecuteAsync(),
                Cron.Minutely);

            return services;
        }

    }
}
