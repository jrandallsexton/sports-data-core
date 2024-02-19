using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SportsData.Core.Eventing.Publishers
{
    public class EventPublisherService : BackgroundService
    {
        private const int DelayInSeconds = 10;
        private const int MillisecondsToSeconds = 1000;

        private readonly ILogger<EventPublisherService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private IEventPublisher _eventPublisher;

        public EventPublisherService(ILogger<EventPublisherService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Begin ExecuteAsync");

            using var serviceScope = _serviceProvider.CreateScope();
            _eventPublisher = serviceScope.ServiceProvider.GetService<IEventPublisher>();

            while (!cancellationToken.IsCancellationRequested)
            {
                await _eventPublisher.PublishAsync(cancellationToken);
                await Task.Delay(DelayInSeconds * MillisecondsToSeconds, cancellationToken);
            }
        }
    }
}
