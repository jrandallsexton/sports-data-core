using SportsData.Core.DependencyInjection;
using SportsData.Producer.Infrastructure.Data;

using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var config = builder.Configuration;
config.AddCommonConfiguration(builder.Environment.EnvironmentName, builder.Environment.ApplicationName);

var services = builder.Services;
services.AddCoreServices(config);
services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

// Add Serilog
builder.UseCommon();

services.AddProviders(config);
services.AddDataPersistence<AppDataContext>(config, builder.Environment.ApplicationName);
services.AddMessaging(config);

services.AddHealthChecks<AppDataContext>(Assembly.GetExecutingAssembly().GetName(false).Name);

var app = builder.Build();

// Configure the HTTP request pipeline.
//app.UseHttpsRedirection();

app.UseAuthorization();

app.UseCommonFeatures();

app.MapControllers();

app.Run();
