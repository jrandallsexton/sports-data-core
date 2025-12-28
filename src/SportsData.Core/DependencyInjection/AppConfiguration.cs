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
using System.IO;
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

                // NOTE: ReadFrom.Configuration is commented out to prevent duplicate sinks
                // All sinks should be configured explicitly below
                // configuration.ReadFrom.Configuration(context.Configuration);

                // Environment-aware file logging path
                var logPath = GetLogFilePath(context.HostingEnvironment);
                
                // Hardcoded File Sink for Feedback Loop
                configuration.WriteTo.File(
                    path: logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 3,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
                );

                // Enrich
                configuration
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("ApplicationName", context.HostingEnvironment.ApplicationName)
                    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName);

                // Seq sink (optional)
                if (!string.IsNullOrWhiteSpace(seqUri))
                {
                    var seqLevel = ParseLevel(loggingConfig.SeqMinimumLevel, globalLevel);
                    configuration.WriteTo.Seq(seqUri, restrictedToMinimumLevel: seqLevel);
                }

                // Serilog internal debug (optional)
                //Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));
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
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    var links = new StringBuilder();
                    links.AppendLine($"<a Ref=\"\" target=\"_blank\">Environment: {app.Environment.EnvironmentName}</a></br>");
                    links.AppendLine($"<a Ref=\"\" target=\"_blank\">BuildConfig: {buildConfiguration}</a></br>");
                    links.AppendLine("<a Ref=\"/health\" target=\"_blank\">HealthCheck</a></br>");
                    links.AppendLine("<a Ref=\"/dashboard\" target=\"_blank\">Hangfire</a></br>");
                    links.AppendLine("<a Ref=\"/metrics\" target=\"_blank\">Metrics</a></br>");

                    options.HeadContent = links.ToString();
                });
            }
            else
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

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

        private static LogEventLevel ParseLevel(string? value, LogEventLevel fallback)
        {
            return Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var level) ? level : fallback;
        }

        /// <summary>
        /// Determines the appropriate log file path based on the environment.
        /// - Windows (Development): C:\Projects\sports-data\logs\{AppName}-{Date}.log
        /// - Linux/Docker (Production): /app/logs/{AppName}-{Date}.log (requires volume mount)
        /// - Environment variable override: LOG_PATH (highest priority)
        /// </summary>
        private static string GetLogFilePath(IHostEnvironment env)
        {
            var appName = env.ApplicationName;
            
            // 1. Check for environment variable override (highest priority)
            var envLogPath = Environment.GetEnvironmentVariable("LOG_PATH");
            if (!string.IsNullOrWhiteSpace(envLogPath))
            {
                var resolvedPath = envLogPath.Replace("{AppName}", appName);
                var dir = Path.GetDirectoryName(resolvedPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return resolvedPath;
            }

            // 2. Auto-detect based on OS and environment
            if (OperatingSystem.IsWindows() && env.IsDevelopment())
            {
                // Windows Development: C:\Projects\sports-data\logs\
                var logDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "logs");
                var absoluteLogDir = Path.GetFullPath(logDir);
                
                if (!Directory.Exists(absoluteLogDir))
                {
                    Directory.CreateDirectory(absoluteLogDir);
                }
                
                return Path.Combine(absoluteLogDir, $"{appName}-.log");
            }
            else
            {
                // Linux/Docker: /app/logs/ (must be mounted as volume in docker-compose/k8s)
                var logDir = "/app/logs";
                
                // Try to create directory (will succeed if volume is mounted with write permissions)
                try
                {
                    if (!Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }
                    return Path.Combine(logDir, $"{appName}-{Environment.MachineName}-.log");
                }
                catch
                {
                    // Fallback to /tmp if /app/logs is not writable
                    var fallbackDir = "/tmp/logs";
                    if (!Directory.Exists(fallbackDir))
                    {
                        Directory.CreateDirectory(fallbackDir);
                    }
                    return Path.Combine(fallbackDir, $"{appName}-{Environment.MachineName}-.log");
                }
            }
        }
    }
}