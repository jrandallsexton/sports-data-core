using MassTransit;

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
        private readonly IBus _bus;

        public HeartbeatPublisher(IBus bus,
            ILogger<HeartbeatPublisher<T>> logger)
        {
            _bus = bus;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await _bus.Publish(new Heartbeat()
                {
                    CreatedAt = DateTime.UtcNow,
                    Producer = typeof(T).FullName
                }, stoppingToken);
                _logger.LogInformation("heartbeat sent from {@t}", typeof(T));
                await Task.Delay(60_000, stoppingToken);
            }
        }
    }
}
