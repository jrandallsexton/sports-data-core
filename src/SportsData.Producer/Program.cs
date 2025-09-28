using Hangfire;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Documents;
using SportsData.Producer.Application.Images.Handlers;
using SportsData.Producer.DependencyInjection;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Golf;
using SportsData.Producer.Mapping;

using System.Reflection;

namespace SportsData.Producer;

public class Program
{
    public static async Task Main(string[] args)
    {
        var mode = (args.Length > 0 && args[0] == "-mode") ?
            Enum.Parse<Sport>(args[1]) :
            Sport.All;

        Console.WriteLine($"Mode: {mode}");

        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        var config = builder.Configuration;
        config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName, mode);
        
        builder.UseCommon();

        var services = builder.Services;
        services.AddCoreServices(config, mode);
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddClients(config);
        
        switch (mode)
        {
            case Sport.GolfPga:
                services.AddDataPersistence<GolfDataContext>(config, builder.Environment.ApplicationName, mode);
                break;
            case Sport.FootballNcaa:
            case Sport.FootballNfl:
                services.AddDataPersistence<FootballDataContext>(config, builder.Environment.ApplicationName, mode);
                services.AddScoped<FootballDataContext>();
                services.AddScoped<TeamSportDataContext, FootballDataContext>();
                services.AddScoped<BaseDataContext, FootballDataContext>();
                break;
            case Sport.All:
            case Sport.BaseballMlb:
            case Sport.BasketballNba:
            default:
                throw new ArgumentOutOfRangeException();
        }

        services.AddHangfire(config, builder.Environment.ApplicationName, mode, 24);

        // Add messaging via MassTransit using Outbox pattern
        //services.AddMessaging<BaseDataContext, TeamSportDataContext, FootballDataContext>(config, [
        //    typeof(DocumentCreatedHandler),
        //    typeof(ProcessImageRequestedHandler),
        //    typeof(ProcessImageResponseHandler)
        //]);

        // Add messaging via MassTransit WITHOUT using Outbox pattern
        services.AddMessaging(config, [
            typeof(DocumentCreatedHandler),
            typeof(ProcessImageRequestedHandler),
            typeof(ProcessImageResponseHandler)
        ]);

        //services.AddMessaging<BaseDataContext, TeamSportDataContext, FootballDataContext>(
        //    config,
        //    [
        //        typeof(DocumentCreatedHandler),
        //        typeof(ProcessImageRequestedHandler),
        //        typeof(ProcessImageResponseHandler)
        //    ]);

        services.AddInstrumentation(builder.Environment.ApplicationName);

        switch (mode)
        {
            case Sport.GolfPga:
                services.AddHealthChecks<GolfDataContext, Program>(builder.Environment.ApplicationName, mode);
                break;
            case Sport.FootballNcaa:
            case Sport.FootballNfl:
                services.AddHealthChecks<FootballDataContext, Program>(builder.Environment.ApplicationName, mode);
                break;
            case Sport.All:
            case Sport.BaseballMlb:
            case Sport.BasketballNba:
            default:
                throw new ArgumentOutOfRangeException();
        }

        services.AddLocalServices(mode);

        var hostAssembly = Assembly.GetExecutingAssembly();
        services.AddAutoMapper(typeof(MappingProfile));
        services.AddMediatR(hostAssembly);

        var app = builder.Build();
        
        app.UseHttpsRedirection();

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
