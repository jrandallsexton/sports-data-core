using MassTransit;

using SportsData.Core.Eventing.Events;

namespace SportsData.Contest.Infrastructure
{
    public class HeartbeatPublisher : BackgroundService
    {
        private readonly ILogger<HeartbeatPublisher> _logger;
        private readonly IBus _bus;

        public HeartbeatPublisher(IBus bus,
            ILogger<HeartbeatPublisher> logger)
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
                    Producer = nameof(HeartbeatPublisher)
                }, stoppingToken);
                _logger.LogInformation("heartbeat sent");
                await Task.Delay(30_000, stoppingToken);
            }
        }
    }
}
