using Azure.Identity;

using Google.Protobuf.WellKnownTypes;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

using SportsData.Core.Common;
using SportsData.Core.Config;
using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace SportsData.Core.DependencyInjection
{
    public static class AppConfiguration
    {
        private const string AppConfigLabel = "APPCONFIG_LABEL";
        private const string AppConfigConnString = "APPCONFIG_CONNSTR";

        public static WebApplicationBuilder UseCommon(this WebApplicationBuilder builder)
        {
            builder.Host.UseSerilog((context, configuration) =>
            {
                var seqUri = context.Configuration["CommonConfig:SeqUri"];
                Console.WriteLine($"[DEBUG] SeqUri from config: {seqUri}");

                configuration
                    // Global minimum level
                    .MinimumLevel.Information()

                    // Per-namespace overrides
                    .MinimumLevel.Override("SportsData", LogEventLevel.Information)
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("System", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                    //.ReadFrom.Configuration(context.Configuration)

                    // Enrich
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("ApplicationName", context.HostingEnvironment.ApplicationName)

                    // Sinks
                    .WriteTo.Console(); // Optional, for local debug

                if (!string.IsNullOrWhiteSpace(seqUri))
                {
                    configuration.WriteTo.Seq(seqUri, restrictedToMinimumLevel: LogEventLevel.Verbose);
                }

                Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));


                //.WriteTo.OpenTelemetry(options =>
                //{
                //    // TODO: Move this to az app config
                //    options.Endpoint = "http://localhost:4317/v1/logs";
                //    options.Protocol = OtlpProtocol.Grpc;
                //    options.ResourceAttributes = new Dictionary<string, object>
                //    {
                //        ["service.name"] = builder.Environment.ApplicationName
                //    };
                //});
            });

            //builder.Logging.AddOpenTelemetry(x =>
            //{
            //    x.IncludeScopes = true;
            //    x.IncludeFormattedMessage = true;
            //});

            return builder;
        }

        public static WebApplication UseCommonFeatures(this WebApplication app, string buildConfiguration = "Debug")
        {
            app.UseHealthChecks("/api/health", new HealthCheckOptions()
            {
                ResponseWriter = HealthCheckWriter.WriteResponse
            });

            if (app.Environment.ApplicationName == "SportsData.Api")
            {
                //if (app.Environment.IsDevelopment() ||
                //    app.Environment.EnvironmentName == "Local" ||
                //    app.Environment.EnvironmentName == "Dev")
                //{
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    var links = new StringBuilder();
                    links.AppendLine($"<a Href=\"\" target=\"_blank\">Environment: {app.Environment.EnvironmentName}</a></br>");
                    links.AppendLine($"<a Href=\"\" target=\"_blank\">BuildConfig: {buildConfiguration}</a></br>");
                    links.AppendLine("<a Href=\"/health\" target=\"_blank\">HealthCheck</a></br>");
                    links.AppendLine("<a Href=\"/dashboard\" target=\"_blank\">Hangfire</a></br>");
                    links.AppendLine("<a Href=\"/metrics\" target=\"_blank\">Metrics</a></br>");

                    //if (string.Equals(app.Environment.EnvironmentName, "Local", StringComparison.InvariantCultureIgnoreCase))
                    //{
                    //    links.AppendLine("<a Href=\"http://localhost:30084/?orgId=1\" target=\"_blank\">Grafana</a></br>");
                    //}
                    //else
                    //{
                    //    links.AppendLine("<a Href=\"http://localhost:3000/?orgId=1\" target=\"_blank\">Grafana</a></br>");
                    //}

                    //if (string.Equals(app.Environment.EnvironmentName, "Local", StringComparison.InvariantCultureIgnoreCase))
                    //{
                    //    links.AppendLine("<a Href=\"http://localhost:30083/graph\" target=\"_blank\">Prometheus</a></br>");
                    //}
                    //else
                    //{
                    //    links.AppendLine("<a Href=\"http://localhost:9090/graph\" target=\"_blank\">Prometheus</a></br>");
                    //}

                    //if (string.Equals(app.Environment.EnvironmentName, "Local", StringComparison.InvariantCultureIgnoreCase))
                    //{
                    //    links.AppendLine("<a Href=\"http://localhost:30082/redis-stack/browser\" target=\"_blank\">Redis</a></br>");
                    //}
                    //else
                    //{
                    //    links.AppendLine("<a Href=\"http://localhost:8001/redis-stack/browser\" target=\"_blank\">Redis</a></br>");
                    //}

                    //if (string.Equals(app.Environment.EnvironmentName, "Local", StringComparison.InvariantCultureIgnoreCase))
                    //{
                    //    links.AppendLine("<a Href=\"http://localhost:30081/#/events?range=1d\" target=\"_blank\">Seq</a></br>");
                    //}
                    //else
                    //{
                    //    links.AppendLine("<a Href=\"http://localhost:8090/#/events?range=1d\" target=\"_blank\">Seq</a></br>");
                    //}

                    options.HeadContent = links.ToString();
                });
                //}
            }
            else
            {
                if (app.Environment.IsDevelopment() ||
                    app.Environment.EnvironmentName == "Local")
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }
            }

            //app.UseSerilogRequestLogging();
            //app.MapPrometheusScrapingEndpoint();

            return app;
        }

        public static ConfigurationManager AddCommonConfiguration(
            this ConfigurationManager cfg,
            string environmentName,
            string applicationName,
            Sport mode = Sport.All)
        {
            cfg.AddUserSecrets(Assembly.GetExecutingAssembly());
            cfg.AddUserSecrets(Assembly.GetCallingAssembly());
            cfg.AddUserSecrets<CommonConfig>();

            var label = cfg[AppConfigLabel] ?? Environment.GetEnvironmentVariable(AppConfigLabel);

            var appConfigConnectionString = cfg[AppConfigConnString];

            if (string.IsNullOrEmpty(appConfigConnectionString))
                appConfigConnectionString = cfg.GetSection("AzAppConfigConnString").Value;

            if (string.IsNullOrWhiteSpace(appConfigConnectionString))
                throw new InvalidOperationException($"Missing AppConfig connection string. Set {AppConfigConnString}.");

            cfg.AddAzureAppConfiguration(azAppConfig =>
            {
                azAppConfig.Connect(appConfigConnectionString)
                    // Shared values
                    .Select("CommonConfig", string.Empty)
                    .Select("CommonConfig", label)
                    .Select("CommonConfig:*", label);

                if (mode == Sport.All)
                {
                    // Load SupportedModes from AppConfig under CommonConfig:Api
                    var localTempConfig = new ConfigurationBuilder()
                        .AddAzureAppConfiguration(options =>
                            options.Connect(appConfigConnectionString)
                                   .Select("CommonConfig:Api:SupportedModes", $"{label}.{mode}"))
                        .Build();

                    var supportedModes = localTempConfig
                        .GetSection("CommonConfig:Api:SupportedModes")
                        .Get<List<string>>() ?? new();

                    foreach (var supportedMode in supportedModes)
                    {
                        azAppConfig
                            .Select("CommonConfig:*", $"{label}.{supportedMode}")
                            .Select($"{applicationName}:*", $"{label}.{supportedMode}");
                    }
                }
                else
                {
                    azAppConfig
                        .Select("CommonConfig:*", $"{label}.{mode}")
                        .Select($"{applicationName}:*", $"{label}.{mode}");
                }

                azAppConfig
                    .Select($"{applicationName}:*", label)
                    .Select(applicationName, label)
                    .ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential());
                    });
            });

            return cfg;
        }

    }
}