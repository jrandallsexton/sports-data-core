using Azure.Identity;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using Serilog;
using Serilog.Events;

using SportsData.Core.Common;
using SportsData.Core.Config;
using SportsData.Core.Middleware.Health;

using System;
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
                var loggingSection = context.Configuration.GetSection("CommonConfig:Logging");
                var loggingConfig = loggingSection.Get<CommonConfig.LoggingConfig>() ?? new CommonConfig.LoggingConfig();

                var seqUri = context.Configuration["CommonConfig:SeqUri"];

                // Parse global minimum level
                var globalLevel = ParseLevel(loggingConfig.MinimumLevel, LogEventLevel.Information);
                configuration.MinimumLevel.Is(globalLevel);

                // Parse and apply overrides
                foreach (var entry in loggingConfig.Overrides)
                {
                    var level = ParseLevel(entry.Value, LogEventLevel.Warning);
                    configuration.MinimumLevel.Override(entry.Key, level);
                }

                // Enrich
                configuration
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("ApplicationName", context.HostingEnvironment.ApplicationName);

                // Seq sink (optional)
                if (!string.IsNullOrWhiteSpace(seqUri))
                {
                    var seqLevel = ParseLevel(loggingConfig.SeqMinimumLevel, globalLevel);
                    configuration.WriteTo.Seq(seqUri, restrictedToMinimumLevel: seqLevel);
                }

                // Serilog internal debug (optional)
                Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));
            });

            return builder;

            static LogEventLevel ParseLevel(string? value, LogEventLevel fallback)
            {
                return Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var level) ? level : fallback;
            }
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
                    links.AppendLine($"<a Ref=\"\" target=\"_blank\">Environment: {app.Environment.EnvironmentName}</a></br>");
                    links.AppendLine($"<a Ref=\"\" target=\"_blank\">BuildConfig: {buildConfiguration}</a></br>");
                    links.AppendLine("<a Ref=\"/health\" target=\"_blank\">HealthCheck</a></br>");
                    links.AppendLine("<a Ref=\"/dashboard\" target=\"_blank\">Hangfire</a></br>");
                    links.AppendLine("<a Ref=\"/metrics\" target=\"_blank\">Metrics</a></br>");

                    //if (string.Equals(app.Environment.EnvironmentName, "Local", StringComparison.InvariantCultureIgnoreCase))
                    //{
                    //    links.AppendLine("<a Ref=\"http://localhost:30084/?orgId=1\" target=\"_blank\">Grafana</a></br>");
                    //}
                    //else
                    //{
                    //    links.AppendLine("<a Ref=\"http://localhost:3000/?orgId=1\" target=\"_blank\">Grafana</a></br>");
                    //}

                    //if (string.Equals(app.Environment.EnvironmentName, "Local", StringComparison.InvariantCultureIgnoreCase))
                    //{
                    //    links.AppendLine("<a Ref=\"http://localhost:30083/graph\" target=\"_blank\">Prometheus</a></br>");
                    //}
                    //else
                    //{
                    //    links.AppendLine("<a Ref=\"http://localhost:9090/graph\" target=\"_blank\">Prometheus</a></br>");
                    //}

                    //if (string.Equals(app.Environment.EnvironmentName, "Local", StringComparison.InvariantCultureIgnoreCase))
                    //{
                    //    links.AppendLine("<a Ref=\"http://localhost:30082/redis-stack/browser\" target=\"_blank\">Redis</a></br>");
                    //}
                    //else
                    //{
                    //    links.AppendLine("<a Ref=\"http://localhost:8001/redis-stack/browser\" target=\"_blank\">Redis</a></br>");
                    //}

                    //if (string.Equals(app.Environment.EnvironmentName, "Local", StringComparison.InvariantCultureIgnoreCase))
                    //{
                    //    links.AppendLine("<a Ref=\"http://localhost:30081/#/events?range=1d\" target=\"_blank\">Seq</a></br>");
                    //}
                    //else
                    //{
                    //    links.AppendLine("<a Ref=\"http://localhost:8090/#/events?range=1d\" target=\"_blank\">Seq</a></br>");
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

            var label = cfg[AppConfigLabel] ?? Environment.GetEnvironmentVariable(AppConfigLabel) ?? "Local";

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
                    .Select("CommonConfig:*", label)
                    .Select("CommonConfig:*", $"{label}.{mode}.{applicationName}");

                azAppConfig
                    .Select("CommonConfig:*", $"{label}.{mode}")
                    .Select($"{applicationName}:*", $"{label}.{mode}");

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