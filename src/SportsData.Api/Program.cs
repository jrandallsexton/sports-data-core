using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

using Hangfire;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

using SportsData.Api.Application;
using SportsData.Api.Infrastructure;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Middleware.Health;

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SportsData.Api.Application.Auth;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Config;

namespace SportsData.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Initialize Firebase Admin SDK
            var credentialPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");

            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile(credentialPath)
            });


            // 2. Configure JWT Authentication
            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = "https://securetoken.google.com/sportdeets-dev";
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = "https://securetoken.google.com/sportdeets-dev",
                        ValidateAudience = true,
                        ValidAudience = "sportdeets-dev",
                        ValidateLifetime = true
                    };
                });

            builder.Services.AddControllers(options =>
            {
                options.ModelBinderProviders.Insert(0, new FirebaseUserClaimsBinderProvider());
            });


            // 3. Add Authorization middleware
            builder.Services.AddAuthorization();

            builder.UseCommon();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins("http://localhost:3000")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials(); // if using cookies or Authorization header
                });
            });


            // Add services to the container.
            var config = builder.Configuration;
            config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName);

            var services = builder.Services;
            services.Configure<CommonConfig>(config.GetSection("CommonConfig"));
            services.AddCoreServices(config);
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddProviders(config);
            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName);
            //services.AddMessaging(config, [typeof(HeartbeatConsumer)]);
            //services.AddInstrumentation(builder.Environment.ApplicationName);
            //services.AddHangfire(x => x.UseSqlServerStorage(config[$"{builder.Environment.ApplicationName}:ConnectionStrings:Hangfire"]));
            services.AddCaching(config);
            services.AddHealthChecksMaster(builder.Environment.ApplicationName);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseHttpsRedirection();
            
            using (var scope = app.Services.CreateScope())
            {
                var appServices = scope.ServiceProvider;
                var dbContext = appServices.GetRequiredService<AppDataContext>();
                await dbContext.Database.MigrateAsync();
            }

            //app.UseHangfireDashboard("/dashboard", new DashboardOptions
            //{
            //    Authorization = new[] { new DashboardAuthFilter() }
            //});

            app.UseCors("AllowFrontend");
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseHealthChecks("/health", new HealthCheckOptions()
            {
                ResponseWriter = HealthCheckWriter.WriteResponse
            });

            app.MapControllers();

            var assemblyConfigurationAttribute = typeof(Program).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
            var buildConfigurationName = assemblyConfigurationAttribute?.Configuration;

            app.UseCommonFeatures(buildConfigurationName);

            app.Run();
        }
    }
}
