using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Sinks.OpenTelemetry;

using SportsData.Core.Common;
using SportsData.Core.Config;
using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SportsData.Core.DependencyInjection
{
    public static class AppConfiguration
    {
        public static WebApplicationBuilder UseCommon(this WebApplicationBuilder builder)
        {
            //builder.Host.UseSerilog((context, configuration) =>
            //{
            //    configuration
            //        .ReadFrom.Configuration(context.Configuration)
            //        .Enrich.FromLogContext()
            //        .Enrich.WithProperty("ApplicationName", context.HostingEnvironment.ApplicationName)
            //        .WriteTo.OpenTelemetry(options =>
            //        {
            //            // TODO: Move this to az app config
            //            options.Endpoint = "http://localhost:4317/v1/logs";
            //            options.Protocol = OtlpProtocol.Grpc;
            //            options.ResourceAttributes = new Dictionary<string, object>
            //            {
            //                ["service.name"] = builder.Environment.ApplicationName
            //            };
            //        });
            //});

            //builder.Logging.AddOpenTelemetry(x =>
            //{
            //    x.IncludeScopes = true;
            //    x.IncludeFormattedMessage = true;
            //});

            return builder;
        }

        public static WebApplication UseCommonFeatures(this WebApplication app, string buildConfiguration = "Debug")
        {
            Console.WriteLine("UseCommonFeatures!");

            app.UseHealthChecks("/api/health", new HealthCheckOptions()
            {
                ResponseWriter = HealthCheckWriter.WriteResponse
            });

            if (app.Environment.ApplicationName == "SportsData.Api")
            {
                if (app.Environment.IsDevelopment() ||
                    app.Environment.EnvironmentName == "Local")
                {
                    app.UseSwagger();
                    app.UseSwaggerUI(options =>
                    {
                        var links = new StringBuilder();
                        links.AppendLine($"<a href=\"\" target=\"_blank\">Environment: {app.Environment.EnvironmentName}</a></br>");
                        links.AppendLine($"<a href=\"\" target=\"_blank\">BuildConfig: {buildConfiguration}</a></br>");
                        links.AppendLine("<a href=\"/health\" target=\"_blank\">HealthCheck</a></br>");
                        links.AppendLine("<a href=\"/dashboard\" target=\"_blank\">Hangfire</a></br>");
                        links.AppendLine("<a href=\"/metrics\" target=\"_blank\">Metrics</a></br>");

                        if (string.Equals(app.Environment.EnvironmentName, "Local", StringComparison.InvariantCultureIgnoreCase))
                        {
                            links.AppendLine("<a href=\"http://localhost:30084/?orgId=1\" target=\"_blank\">Grafana</a></br>");
                        }
                        else
                        {
                            links.AppendLine("<a href=\"http://localhost:3000/?orgId=1\" target=\"_blank\">Grafana</a></br>");
                        }

                        if (string.Equals(app.Environment.EnvironmentName, "Local", StringComparison.InvariantCultureIgnoreCase))
                        {
                            links.AppendLine("<a href=\"http://localhost:30083/graph\" target=\"_blank\">Prometheus</a></br>");
                        }
                        else
                        {
                            links.AppendLine("<a href=\"http://localhost:9090/graph\" target=\"_blank\">Prometheus</a></br>");
                        }

                        if (string.Equals(app.Environment.EnvironmentName, "Local", StringComparison.InvariantCultureIgnoreCase))
                        {
                            links.AppendLine("<a href=\"http://localhost:30082/redis-stack/browser\" target=\"_blank\">Redis</a></br>");
                        }
                        else
                        {
                            links.AppendLine("<a href=\"http://localhost:8001/redis-stack/browser\" target=\"_blank\">Redis</a></br>");
                        }

                        if (string.Equals(app.Environment.EnvironmentName, "Local", StringComparison.InvariantCultureIgnoreCase))
                        {
                            links.AppendLine("<a href=\"http://localhost:30081/#/events?range=1d\" target=\"_blank\">Seq</a></br>");
                        }
                        else
                        {
                            links.AppendLine("<a href=\"http://localhost:8090/#/events?range=1d\" target=\"_blank\">Seq</a></br>");
                        }

                        options.HeadContent = links.ToString();
                    });
                }
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
            cfg.AddJsonFile("secrets.json", true);

            var appConfigConnectionString = Environment.GetEnvironmentVariable("APPCONFIG_CONNSTR");
            if (string.IsNullOrEmpty(appConfigConnectionString))
                appConfigConnectionString = cfg.GetSection("AzAppConfigConnString").Value;

            cfg.AddAzureAppConfiguration(azAppConfig =>
            {
                azAppConfig.Connect(appConfigConnectionString)
                    .Select("CommonConfig", environmentName)
                    .Select("CommonConfig", $"{environmentName}.{mode}")
                    .Select(applicationName, environmentName)
                    .Select(applicationName, $"{environmentName}.{mode}");
            });

            // TODO: Determine a better way of doing this
            cfg.AddUserSecrets(Assembly.GetExecutingAssembly());
            cfg.AddUserSecrets(Assembly.GetCallingAssembly());
            cfg.AddUserSecrets<CommonConfig>();
            return cfg;
        }
    }
}