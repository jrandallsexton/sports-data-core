using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Infrastructure.Data;

namespace SportsData.Notification
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var mode = CommandLineHelpers.ParseFlag<Sport>(args, "-mode", Sport.All);

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
            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName, mode);

            services.AddMessaging(config, [
                typeof(ContestStartTimeUpdatedConsumer),
                typeof(PickemGroupCreatedConsumer),
                typeof(PickemGroupMatchupCreatedConsumer),
                typeof(PickemGroupMemberAddedConsumer),
                typeof(UserPickScoredConsumer)
            ]);

            services.AddInstrumentation(builder.Environment.ApplicationName, config);
            services.AddHealthChecks<AppDataContext>(builder.Environment.ApplicationName, mode);

            var app = builder.Build();

            await app.Services.ApplyMigrations<AppDataContext>();

            // Configure the HTTP request pipeline.
            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseCommonFeatures();

            app.MapControllers();

            await app.RunAsync();
        }
    }
}
