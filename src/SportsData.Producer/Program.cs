using SportsData.Core.DependencyInjection;

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SportsData.Producer.Infrastructure.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// TODO: Make this follow the same pattern as the other services
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Serilog
builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});

// TODO: Find a way to move this to middleware for all services
builder.Services.AddDbContext<AppDataContext>(options =>
{
    options.EnableSensitiveDataLogging();
    options.UseSqlServer(builder.Configuration.GetConnectionString("AppDataContext"));
});

builder.Services.AddHealthChecks<AppDataContext>(Assembly.GetExecutingAssembly().GetName(false).Name);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.UseCommonFeatures();

app.MapControllers();

app.Run();
