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
        private readonly IEventConsumer _eventConsumer;

        public EventConsumerService(
            ILogger<EventConsumerService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;

            using var serviceScope = serviceProvider.CreateScope();

            if (serviceScope is null)
            {
                throw new ArgumentNullException(nameof(serviceScope), "Service scope cannot be null.");
            }

            if (serviceScope.ServiceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceScope.ServiceProvider), "Service provider cannot be null.");
            }

            var consumer = serviceScope.ServiceProvider.GetService<IEventConsumer>();

            _eventConsumer = consumer ?? throw new InvalidOperationException("Event consumer service is not registered.");
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Begin ExecuteAsync");

            while (!cancellationToken.IsCancellationRequested)
            {
                await _eventConsumer.ConsumeAsync(cancellationToken);
                await Task.Delay(DelayInSeconds * MillisecondsToSeconds, cancellationToken);
            }
        }
    }
}
