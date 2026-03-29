using System.Text.RegularExpressions;

using Hangfire;
using Hangfire.PostgreSql;

using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;

namespace SportsData.JobsDashboard;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.UseCommon();

        var config = builder.Configuration;
        config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName);

        var services = builder.Services;
        services.AddHealthChecks();

        // Register Hangfire middleware infrastructure only — no server, no workers.
        // Each dashboard path is backed by a separate JobStorage instance below.
        services.AddHangfire(x => { });

        var app = builder.Build();

        var sqlBase = config["CommonConfig:SqlBaseConnectionString"]
            ?? throw new InvalidOperationException(
                "CommonConfig:SqlBaseConnectionString is required. Ensure Azure AppConfig is configured.");

        // Strip any existing pool size from the base string, then set a small pool —
        // the dashboard is read-only and needs very few connections
        var cleanBase = Regex.Replace(sqlBase, @"Maximum Pool Size=\d+;?", string.Empty,
            RegexOptions.IgnoreCase).TrimEnd(';');

        // Pool=5 per storage — dashboard is read-only and makes concurrent stat queries
        const string poolOpts = "Maximum Pool Size=5;Timeout=15;Command Timeout=30";

        var dashboardOptions = new DashboardOptions
        {
            Authorization = [new DashboardAuthFilter()],
            DashboardTitle = "sportDeets — Job Dashboards"
        };

        // NCAA Football
        var ncaaProviderStorage = new PostgreSqlStorage($"{cleanBase};Database=sdProvider.FootballNcaa.Hangfire;{poolOpts}");
        var ncaaProducerStorage = new PostgreSqlStorage($"{cleanBase};Database=sdProducer.FootballNcaa.Hangfire;{poolOpts}");
        app.UseHangfireDashboard("/footballncaa/provider", dashboardOptions, ncaaProviderStorage);
        app.UseHangfireDashboard("/footballncaa/producer", dashboardOptions, ncaaProducerStorage);

        // NFL Football
        var nflProviderStorage = new PostgreSqlStorage($"{cleanBase};Database=sdProvider.FootballNfl.Hangfire;{poolOpts}");
        var nflProducerStorage = new PostgreSqlStorage($"{cleanBase};Database=sdProducer.FootballNfl.Hangfire;{poolOpts}");
        app.UseHangfireDashboard("/footballnfl/provider", dashboardOptions, nflProviderStorage);
        app.UseHangfireDashboard("/footballnfl/producer", dashboardOptions, nflProducerStorage);

        // API (shared across all sports)
        var apiStorage = new PostgreSqlStorage($"{cleanBase};Database=sdApi.All.Hangfire;{poolOpts}");
        app.UseHangfireDashboard("/api", dashboardOptions, apiStorage);

        // Root landing page with links to all dashboards
        app.MapGet("/", () => Results.Content(
            """
            <html><head><title>sportDeets Job Dashboards</title></head>
            <body style="font-family:sans-serif;padding:2rem">
            <h2>sportDeets Job Dashboards</h2>
            <h3>NCAA Football</h3>
            <ul>
              <li><a href="/footballncaa/provider">Provider</a></li>
              <li><a href="/footballncaa/producer">Producer</a></li>
            </ul>
            <h3>NFL Football</h3>
            <ul>
              <li><a href="/footballnfl/provider">Provider</a></li>
              <li><a href="/footballnfl/producer">Producer</a></li>
            </ul>
            <h3>Shared</h3>
            <ul>
              <li><a href="/api">API</a></li>
            </ul>
            </body></html>
            """, "text/html"));

        app.UseCommonFeatures();

        await app.RunAsync();
    }
}
