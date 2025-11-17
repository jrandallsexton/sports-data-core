using FirebaseAdmin;

using Google.Apis.Auth.OAuth2;

using Hangfire;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

using Npgsql;

using Serilog;

using SportsData.Api.Application.Auth;
using SportsData.Api.Application.Events;
using SportsData.Api.Application.PickemGroups;
using SportsData.Api.Application.Previews;
using SportsData.Api.DependencyInjection;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Api.Middleware;
using SportsData.Core.Common;
using SportsData.Core.Config;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Infrastructure.Clients.AI;
using SportsData.Core.Middleware.Health;
using SportsData.Core.Processing;

using System.Data;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SportsData.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var mode = (args.Length > 0 && args[0] == "-mode") ?
                Enum.Parse<Sport>(args[1]) :
                Sport.All;

            var builder = WebApplication.CreateBuilder(args);

            // configure JWT Authentication
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
                        ValidateLifetime = true,
                        NameClaimType = "user_id",
                        RoleClaimType = "role"
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                            var cookie = context.Request.Cookies["authToken"];
                            logger.LogInformation("JWT OnMessageReceived - Cookie present: {HasCookie}, Path: {Path}, Method: {Method}",
                                !string.IsNullOrEmpty(cookie),
                                context.Request.Path,
                                context.Request.Method);

                            // Get the token from the cookie
                            context.Token = cookie;
                            return Task.CompletedTask;
                        },
                        OnAuthenticationFailed = context =>
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                            logger.LogError(context.Exception, "Authentication failed for request to {Path}", context.Request.Path);
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                            logger.LogInformation("Token validated for user {UserId} on path {Path}",
                                context.Principal!.FindFirst("user_id")?.Value,
                                context.Request.Path);
                            return Task.CompletedTask;
                        }
                    };
                });

            builder.Services.AddControllers(options =>
            {
                options.ModelBinderProviders.Insert(0, new FirebaseUserClaimsBinderProvider());
            });

            // 3. Add Authorization middleware
            //builder.Services.AddAuthorization();

            builder.UseCommon();

            // Add services to the container.
            var config = builder.Configuration;
            config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName);

            var services = builder.Services;
            services.Configure<CommonConfig>(config.GetSection("CommonConfig"));
            services.Configure<NotificationConfig>(config.GetSection("CommonConfig:NotificationConfig"));
            
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromJson(config["CommonConfig:FirebaseConfigJson"])
            });

            builder.Services.AddScoped<IDbConnection>(sp =>
            {
                var connString = config["SportsData.Api:CanonicalDataProvider:ConnectionString"];
                return new NpgsqlConnection(connString);
            });

            services.AddCoreServices(config);

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            services.AddClients(config);

            /* AI */
            var ollamaConfig = new OllamaClientConfig
            {
                Model = config["CommonConfig:OllamaClientConfig:Model"]!,
                BaseUrl = config["CommonConfig:OllamaClientConfig:BaseUrl"]!
            };
            services.AddSingleton(ollamaConfig);

            services.AddHttpClient<OllamaClient>((sp, client) =>
            {
                var cfg = sp.GetRequiredService<OllamaClientConfig>();
                client.BaseAddress = new Uri(cfg.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(5);
            });

            services.AddSingleton<IProvideAiCommunication>(sp => sp.GetRequiredService<OllamaClient>());
            /* End AI */

            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName, mode);

            services.AddHangfire(config, builder.Environment.ApplicationName, mode, 20);

            services.AddMessaging<AppDataContext>(config,
            [
                typeof(ContestStartTimeUpdatedHandler),
                typeof(PickemGroupCreatedHandler),
                typeof(PickemGroupWeekMatchupsGeneratedHandler),
                typeof(PreviewGeneratedHandler),
                typeof(ContestStatusChangedHandler)
            ]);

            var sigRConnString = config["CommonConfig:AzureSignalR:ConnectionString"];
            services
                .AddSignalR(options =>
                {
                    options.EnableDetailedErrors = true;
                    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
                })
                .AddAzureSignalR(options =>
                {
                    options.ConnectionString = sigRConnString;
                });

            // Configure CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins(
                            "http://localhost:3000",
                            "http://localhost:3001",
                            "http://api-int.sportdeets.com",
                            "https://api-int.sportdeets.com",
                            "https://dev.sportdeets.com",
                            "https://sportdeets.com",
                            "https://www.sportdeets.com")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials() // Required for cookies
                        .WithExposedHeaders("Set-Cookie") // Explicitly expose Set-Cookie header
                        .SetIsOriginAllowedToAllowWildcardSubdomains(); // Allow subdomains
                });
            });

            builder.Services.AddOutputCache();

            services.AddHealthChecksMaster<Program>(builder.Environment.ApplicationName);

            services.AddLocalServices(Sport.All);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            // Don't redirect to HTTPS when behind a proxy (Front Door, Traefik)
            // app.UseHttpsRedirection();

            app.UseCors("AllowFrontend");

            // Ensure CORS headers are added to all responses, including 401/403
            app.Use(async (context, next) =>
            {
                await next();
                
                // Add CORS headers to error responses if not already present
                if ((context.Response.StatusCode == 401 || context.Response.StatusCode == 403) &&
                    context.Request.Headers.ContainsKey("Origin") &&
                    !context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
                {
                    var origin = context.Request.Headers["Origin"].ToString();
                    var allowedOrigins = new[]
                    {
                        "http://localhost:3000",
                        "http://localhost:3001",
                        "http://api-int.sportdeets.com",
                        "https://api-int.sportdeets.com",
                        "https://dev.sportdeets.com",
                        "https://sportdeets.com",
                        "https://www.sportdeets.com"
                    };
                    
                    if (allowedOrigins.Contains(origin))
                    {
                        context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
                        context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
                    }
                }
            });

            app.UseRouting();
            app.UseMiddleware<FirebaseAuthenticationMiddleware>();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseOutputCache();

            await app.Services.ApplyMigrations<AppDataContext>();

            app.UseHealthChecks("/health", new HealthCheckOptions()
            {
                ResponseWriter = HealthCheckWriter.WriteResponse
            });

            app.UseHangfireDashboard("/dashboard", new DashboardOptions
            {
                Authorization = [new DashboardAuthFilter()]
            });

            app.MapControllers();

            var assemblyConfigurationAttribute = typeof(Program).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
            var buildConfigurationName = assemblyConfigurationAttribute?.Configuration ?? "Development";

            app.UseCommonFeatures(buildConfigurationName);

            app.MapHub<NotificationHub>("/hubs/notifications");

            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Azure SignalR registration complete with {connString}", sigRConnString);

            app.Services.ConfigureHangfireJobs(mode);

            await app.RunAsync();
        }
    }
}
