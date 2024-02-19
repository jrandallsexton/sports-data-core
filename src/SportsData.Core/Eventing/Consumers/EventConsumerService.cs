using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Eventing.Consumers
{
    public class EventConsumerService : BackgroundService
    {
        private const int DelayInSeconds = 10;
        private const int MillisecondsToSeconds = 1000;

        private readonly ILogger<EventConsumerService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private IEventConsumer _eventConsumer;

        public EventConsumerService(ILogger<EventConsumerService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Begin ExecuteAsync");

            using var serviceScope = _serviceProvider.CreateScope();
            _eventConsumer = serviceScope.ServiceProvider.GetService<IEventConsumer>();

            while (!cancellationToken.IsCancellationRequested)
            {
                await _eventConsumer.ConsumeAsync(cancellationToken);
                await Task.Delay(DelayInSeconds * MillisecondsToSeconds, cancellationToken);
            }
        }
    }
}
