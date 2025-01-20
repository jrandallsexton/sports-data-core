using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Producer.Application.Documents;
using SportsData.Producer.Application.Handlers;
using SportsData.Producer.DependencyInjection;
using SportsData.Producer.Infrastructure.Data;

using System.Reflection;

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
        services.AddMessaging(config, [
            typeof(DocumentCreatedHandler),
            typeof(ProcessImageRequestedHandler),
            typeof(ProcessImageResponseHandler)]);
        services.AddInstrumentation(builder.Environment.ApplicationName);
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

        app.UseAuthorization();

        app.UseCommonFeatures();

        app.MapControllers();

        await app.RunAsync();
    }
}
