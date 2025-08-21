using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Middleware.Health
{
    public class HeartbeatPublisher<T>(
        IServiceScopeFactory scopeFactory,
        ILogger<HeartbeatPublisher<T>> logger)
        : BackgroundService
        where T : class
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<IEventBus>();

                    await publisher.Publish(new Heartbeat
                    {
                        CreatedAt = DateTime.UtcNow,
                        Producer = typeof(T).FullName ?? "UnknownProducer",
                    }, stoppingToken);

                    logger.LogInformation("Heartbeat published for {Producer}", typeof(T).FullName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to publish heartbeat for {Producer}", typeof(T).FullName);
                }

                await Task.Delay(60_000, stoppingToken);
            }
        }
    }
}