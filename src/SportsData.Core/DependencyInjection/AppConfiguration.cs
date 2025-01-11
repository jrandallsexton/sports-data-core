using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using Serilog;

using SportsData.Core.Config;
using SportsData.Core.Middleware.Health;

using System;
using System.Reflection;
using System.Text;

namespace SportsData.Core.DependencyInjection
{
    public static class AppConfiguration
    {
        public static WebApplicationBuilder UseCommon(this WebApplicationBuilder builder)
        {
            builder.Host.UseSerilog((context, configuration) =>
            {
                configuration.ReadFrom.Configuration(context.Configuration);
            });
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
                        //links.AppendLine("<a href=\"http://localhost:15672/#/\" target=\"_blank\">RabbitMQ</a></br>");

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

                        //links.AppendLine("<a href=\"http://localhost:8888\" target=\"_blank\">pgAdmin</a></br>");
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

            app.UseSerilogRequestLogging();
            return app;
        }

        public static ConfigurationManager AddCommonConfiguration(this ConfigurationManager cfg, string environmentName, string applicationName)
        {
            // TODO: Still need to get this out of the ENV_VAR b/c it is in src for both apps and k8s config. "ok" for now.
            cfg.AddJsonFile("secrets.json", true);

            cfg.AddAzureAppConfiguration(cfg =>
            {
                var cs = Environment.GetEnvironmentVariable("APPCONFIG_CONNSTR");
                cfg.Connect(cs)
                    .Select("CommonConfig", environmentName)
                    .Select(applicationName, environmentName);
            });

            // TODO: Determine a better way of doing this
            cfg.AddUserSecrets(Assembly.GetExecutingAssembly());
            cfg.AddUserSecrets(Assembly.GetCallingAssembly());
            cfg.AddUserSecrets<CommonConfig>();
            return cfg;
        }
    }
}