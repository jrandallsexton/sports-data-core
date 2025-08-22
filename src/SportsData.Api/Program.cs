using FirebaseAdmin;

using Google.Apis.Auth.OAuth2;

using Hangfire;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using Npgsql;

using SportsData.Api.Application.Auth;
using SportsData.Api.Application.PickemGroups;
using SportsData.Api.DependencyInjection;
using SportsData.Api.Infrastructure.Data;
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

            builder.Services.AddScoped<IDbConnection>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                //var connectionString = config.GetConnectionString("CanonicalData");
                var connectionString =
                    "Host=localhost;Port=5432;Username=postgres;Password=sesame1?;Maximum Pool Size=200;Database=sdProducer.FootballNcaa";
                return new NpgsqlConnection(connectionString);
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

            services.AddSingleton(new OllamaClientConfig
            {
                Model = "mistral",
                BaseUrl = "http://localhost:11434"
            });

            services.AddHttpClient<IProvideAiCommunication, OllamaClient>((sp, client) =>
            {
                var config = sp.GetRequiredService<OllamaClientConfig>();
                client.BaseAddress = new Uri(config.BaseUrl);
            });

            services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName, Sport.All);
            services.AddMessaging<AppDataContext>(config, [
                typeof(PickemGroupCreatedHandler),
                typeof(PickemGroupWeekMatchupsGeneratedHandler)
            ]);
            //services.AddInstrumentation(builder.Environment.ApplicationName);
            services.AddHangfire(config, builder.Environment.ApplicationName, mode, 20);
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
            app.UseMiddleware<FirebaseAuthenticationMiddleware>();
            app.UseAuthentication();
            app.UseAuthorization();

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

            app.UseHangfireDashboard("/dashboard", new DashboardOptions
            {
                Authorization = [new DashboardAuthFilter()]
            });

            app.MapControllers();

            var assemblyConfigurationAttribute = typeof(Program).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
            var buildConfigurationName = assemblyConfigurationAttribute?.Configuration ?? "Development";

            app.UseCommonFeatures(buildConfigurationName);

            app.Services.ConfigureHangfireJobs(mode);

            await app.RunAsync();
        }
    }
}
