using FluentValidation;
using FluentValidation.AspNetCore;

using MediatR;

using Microsoft.AspNetCore.Server.Kestrel.Core;

using SportsData.Core.Config;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Middleware;
using SportsData.Venue.Application;
using SportsData.Venue.Application.Handlers;
using SportsData.Venue.Application.Queries;
using SportsData.Venue.Infrastructure.Data;

using System.Net;
using System.Reflection;

namespace SportsData.Venue
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // https://www.reddit.com/r/dotnet/comments/jk6rgg/using_same_microservice_to_deploy_grpc_service/
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                // TODO: Get these ports into AzAppConfig (commonConfig?)
                serverOptions.Listen(IPAddress.Any, 5254, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
                serverOptions.Listen(IPAddress.Any, 5253, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1;
                });
            });

            builder.UseCommon();

            // Add services to the container.
            var config = builder.Configuration;
            config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName);

            var services = builder.Services;
            services.AddGrpc(cfg =>
            {
                cfg.EnableDetailedErrors = true;
            });
            services.Configure<CommonConfig>(config.GetSection("CommonConfig"));
            services.AddCoreServices(config);
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddProviders(config);
            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName);
            services.AddMessaging(config, [typeof(VenueCreatedHandler)]);
            services.AddHealthChecks<AppDataContext>(Assembly.GetExecutingAssembly().GetName(false).Name);
            
            // Add Caching
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = "localhost:6379";
                options.InstanceName = "sdv_"; // (only one app using; good practice)
            });

            var hostAssembly = Assembly.GetExecutingAssembly();
            services.AddAutoMapper(hostAssembly);
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(hostAssembly));
                //.AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryCachingBehavior<,>));
            //builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(hostAssembly))
            //.AddTransient(typeof(IPipelineBehavior<,>), typeof(QueryCachingBehavior<,>))
            //.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddValidatorsFromAssemblyContaining<GetVenueById.Validator>();
            services.AddFluentValidationAutoValidation(cfg =>
            {
                cfg.DisableDataAnnotationsValidation = true;
            });

            // Apply Migrations
            await services.ApplyMigrations<AppDataContext>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            //app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseCommonFeatures();

            //app.UseRouting();
            //app.UseEndpoints(endpoints =>
            //{
            //    endpoints.MapGrpcService<VenueService>();
            //    endpoints.MapControllers();
            //});

            app.MapControllers();
            app.MapGrpcService<VenueService>();

            //app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

            await app.RunAsync();
        }
    }
}
