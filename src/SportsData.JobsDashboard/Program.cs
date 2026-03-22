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

        // TODO: make these configurable via AppConfig when multi-sport support is needed
        var providerStorage = new PostgreSqlStorage($"{cleanBase};Database=sdProvider.FootballNcaa.Hangfire;Maximum Pool Size=2");
        var producerStorage = new PostgreSqlStorage($"{cleanBase};Database=sdProducer.FootballNcaa.Hangfire;Maximum Pool Size=2");
        var apiStorage     = new PostgreSqlStorage($"{cleanBase};Database=sdApi.All.Hangfire;Maximum Pool Size=2");

        var dashboardOptions = new DashboardOptions
        {
            Authorization = [new DashboardAuthFilter()],
            DashboardTitle = "sportDeets — Job Dashboards"
        };

        app.UseHangfireDashboard("/provider", dashboardOptions, providerStorage);
        app.UseHangfireDashboard("/producer", dashboardOptions, producerStorage);
        app.UseHangfireDashboard("/api",      dashboardOptions, apiStorage);

        // Root redirect to provider as a sensible default landing page
        app.MapGet("/", () => Results.Redirect("/provider"));

        app.UseCommonFeatures();

        await app.RunAsync();
    }
}
