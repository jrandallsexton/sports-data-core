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
using Microsoft.Extensions.Logging;
using SportsData.Api.DependencyInjection;
using SportsData.Core.Common;

namespace SportsData.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure JWT Authentication
            //builder.Services
            //    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            //    .AddJwtBearer(options =>
            //    {
            //        options.Authority = "https://securetoken.google.com/sportdeets-dev";
            //        options.TokenValidationParameters = new TokenValidationParameters
            //        {
            //            ValidateIssuer = true,
            //            ValidIssuer = "https://securetoken.google.com/sportdeets-dev",
            //            ValidateAudience = true,
            //            ValidAudience = "sportdeets-dev",
            //            ValidateLifetime = true,
            //            NameClaimType = "user_id",
            //            RoleClaimType = "role"
            //        };
            //        options.Events = new JwtBearerEvents
            //        {
            //            OnMessageReceived = context =>
            //            {
            //                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            //                var cookie = context.Request.Cookies["authToken"];
            //                logger.LogInformation("JWT OnMessageReceived - Cookie present: {HasCookie}, Path: {Path}, Method: {Method}", 
            //                    !string.IsNullOrEmpty(cookie),
            //                    context.Request.Path,
            //                    context.Request.Method);
                            
            //                // Get the token from the cookie
            //                context.Token = cookie;
            //                return Task.CompletedTask;
            //            },
            //            OnAuthenticationFailed = context =>
            //            {
            //                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            //                logger.LogError(context.Exception, "Authentication failed for request to {Path}", context.Request.Path);
            //                return Task.CompletedTask;
            //            },
            //            OnTokenValidated = context =>
            //            {
            //                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            //                logger.LogInformation("Token validated for user {UserId} on path {Path}", 
            //                    context.Principal.FindFirst("user_id")?.Value,
            //                    context.Request.Path);
            //                return Task.CompletedTask;
            //            }
            //        };
            //    });

            builder.Services.AddControllers(options =>
            {
                options.ModelBinderProviders.Insert(0, new FirebaseUserClaimsBinderProvider());
            });

            // 3. Add Authorization middleware
            //builder.Services.AddAuthorization();

            builder.UseCommon();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins(
                            "http://localhost:3000",
                            "https://dev.sportdeets.com")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials() // Required for cookies
                        .WithExposedHeaders("Set-Cookie") // Explicitly expose Set-Cookie header
                        .SetIsOriginAllowedToAllowWildcardSubdomains(); // Allow subdomains
                });
            });

            // Add services to the container.
            var config = builder.Configuration;
            config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName);

            var services = builder.Services;
            services.Configure<CommonConfig>(config.GetSection("CommonConfig"));

            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromJson(config["CommonConfig:FirebaseConfigJson"])
            });

            services.AddCoreServices(config);
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            services.AddClients(config);

            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName, Sport.All);
            //services.AddMessaging(config, [typeof(HeartbeatConsumer)]);
            //services.AddInstrumentation(builder.Environment.ApplicationName);
            //services.AddHangfire(x => x.UseSqlServerStorage(config[$"{builder.Environment.ApplicationName}:ConnectionStrings:Hangfire"]));
            //services.AddCaching(config);
            services.AddHealthChecksMaster(builder.Environment.ApplicationName);

            services.AddLocalServices(Sport.All);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseHttpsRedirection();
            
            using (var scope = app.Services.CreateScope())
            {
                var appServices = scope.ServiceProvider;
                var dbContext = appServices.GetRequiredService<AppDataContext>();
                await dbContext.Database.MigrateAsync();
            }

            app.UseCors("AllowFrontend");

            app.UseRouting();

            //app.UseWhen(context =>
            //        !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) &&
            //        !context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase),
            //    appBuilder =>
            //    {
            //        appBuilder.UseAuthentication();
            //        appBuilder.UseAuthorization();
            //    });

            app.UseHealthChecks("/health", new HealthCheckOptions()
            {
                ResponseWriter = HealthCheckWriter.WriteResponse
            });

            app.MapControllers();

            var assemblyConfigurationAttribute = typeof(Program).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
            var buildConfigurationName = assemblyConfigurationAttribute?.Configuration ?? "Development";

            app.UseCommonFeatures(buildConfigurationName);

            app.Run();
        }
    }
}
