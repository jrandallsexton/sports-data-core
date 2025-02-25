using Hangfire;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Config;
using SportsData.Core.DependencyInjection;
using SportsData.Producer.Application.Documents;
using SportsData.Producer.Application.Images.Handlers;
using SportsData.Producer.DependencyInjection;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Golf;

using System.Reflection;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer;

public class Program
{
    public static async Task Main(string[] args)
    {
        var mode = (args.Length > 0 && args[0] == "-mode") ?
            Enum.Parse<Sport>(args[1]) :
            Sport.GolfPga;

        var builder = WebApplication.CreateBuilder(args);
        builder.UseCommon();

        // Add services to the container.
        var config = builder.Configuration;
        config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName, mode);

        var services = builder.Services;
        services.AddCoreServices(config);
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddProviders(config);

        //services.AddDataPersistence<GolfDataContext>(config, builder.Environment.ApplicationName);
        
        switch (mode)
        {
            case Sport.GolfPga:
                services.AddDataPersistence<GolfDataContext>(config, builder.Environment.ApplicationName);
                break;
            case Sport.FootballNcaa:
            case Sport.FootballNfl:
                services.AddDataPersistence<FootballDataContext>(config, builder.Environment.ApplicationName);
                services.AddScoped<TeamSportDataContext, FootballDataContext>();
                services.AddScoped<BaseDataContext, FootballDataContext>();
                break;
            case Sport.All:
            case Sport.BaseballMlb:
            case Sport.BasketballNba:
            default:
                throw new ArgumentOutOfRangeException();
        }

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            switch (mode)
            {
                case Sport.GolfPga:
                    x.AddEntityFrameworkOutbox<GolfDataContext>(o =>
                    {
                        o.DuplicateDetectionWindow = TimeSpan.FromSeconds(30);
                        o.QueryDelay = TimeSpan.FromSeconds(1);
                        o.UseSqlServer().UseBusOutbox();
                    });
                    break;
                case Sport.FootballNcaa:
                case Sport.FootballNfl:
                    x.AddEntityFrameworkOutbox<FootballDataContext>(o =>
                    {
                        o.DuplicateDetectionWindow = TimeSpan.FromSeconds(30);
                        o.QueryDelay = TimeSpan.FromSeconds(1);
                        o.UseSqlServer().UseBusOutbox();
                    });
                    break;
                case Sport.All:
                case Sport.BaseballMlb:
                case Sport.BasketballNba:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            x.AddConsumer<DocumentCreatedHandler>();
            x.AddConsumer<ProcessImageRequestedHandler>();
            x.AddConsumer<ProcessImageResponseHandler>();

            x.UsingAzureServiceBus((context, cfg) =>
            {
                cfg.Host(config[CommonConfigKeys.AzureServiceBus]);
                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddInstrumentation(builder.Environment.ApplicationName);

        services.AddHangfire(x => x.UseSqlServerStorage(config[$"{builder.Environment.ApplicationName}:ConnectionStrings:Hangfire"]));
        services.AddHangfireServer(serverOptions =>
        {
            // https://codeopinion.com/scaling-hangfire-process-more-jobs-concurrently/
            serverOptions.WorkerCount = 50;
        });

        switch (mode)
        {
            case Sport.GolfPga:
                services.AddHealthChecks<GolfDataContext, Program>(builder.Environment.ApplicationName);
                break;
            case Sport.FootballNcaa:
            case Sport.FootballNfl:
                services.AddHealthChecks<FootballDataContext, Program>(builder.Environment.ApplicationName);
                break;
            case Sport.All:
            case Sport.BaseballMlb:
            case Sport.BasketballNba:
            default:
                throw new ArgumentOutOfRangeException();
        }

        services.AddLocalServices(mode);

        var hostAssembly = Assembly.GetExecutingAssembly();
        services.AddAutoMapper(hostAssembly);
        services.AddMediatR(hostAssembly);

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        //app.UseHttpsRedirection();

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

        app.UseHangfireDashboard("/dashboard", new DashboardOptions
        {
            Authorization = [new DashboardAuthFilter()]
        });

        app.UseAuthorization();

        app.UseCommonFeatures();

        app.MapControllers();

        app.Services.ConfigureHangfireJobs(mode);

        await app.RunAsync();
    }
}
