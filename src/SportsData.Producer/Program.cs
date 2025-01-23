using Hangfire;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Producer.Application.Documents;
using SportsData.Producer.Application.Handlers;
using SportsData.Producer.DependencyInjection;
using SportsData.Producer.Infrastructure.Data;

using System.Reflection;
using SportsData.Core.Config;

namespace SportsData.Producer;

public class Program
{
    public static async Task Main(string[] args)
    {
        var mode = (args.Length > 0 && args[0] == "-mode") ?
            Enum.Parse<Sport>(args[1]) :
            Sport.All;

        var builder = WebApplication.CreateBuilder(args);
        builder.UseCommon();

        // Add services to the container.
        var config = builder.Configuration;
        config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName);

        var services = builder.Services;
        services.AddCoreServices(config);
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddProviders(config);
        services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName);

        //services.AddMessaging(config, [
        //    typeof(DocumentCreatedHandler),
        //    typeof(ProcessImageRequestedHandler),
        //    typeof(ProcessImageResponseHandler)]);

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddEntityFrameworkOutbox<AppDataContext>(o =>
            {
                o.QueryDelay = TimeSpan.FromSeconds(1);
                o.UseSqlServer().UseBusOutbox();
            });

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

        services.AddHealthChecks<AppDataContext, Program>(builder.Environment.ApplicationName);

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
            var context = appServices.GetRequiredService<AppDataContext>();
            await context.Database.MigrateAsync();
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
