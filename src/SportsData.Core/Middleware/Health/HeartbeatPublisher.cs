using MassTransit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SportsData.Core.Eventing.Events;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Middleware.Health
{
    public class HeartbeatPublisher<T> : BackgroundService where T : class
    {
        private readonly ILogger<HeartbeatPublisher<T>> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public HeartbeatPublisher(IServiceScopeFactory scopeFactory,
            ILogger<HeartbeatPublisher<T>> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                    await publisher.Publish(new Heartbeat
                    {
                        CreatedAt = DateTime.UtcNow,
                        Producer = typeof(T).FullName
                    }, stoppingToken);

                    _logger.LogInformation("Heartbeat published for {Producer}", typeof(T).FullName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish heartbeat for {Producer}", typeof(T).FullName);
                }

                await Task.Delay(60_000, stoppingToken);
            }
        }
    }
}