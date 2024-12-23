using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Notification
{
    public interface IProvideNotifications : IProvideHealthChecks
    {

    }

    public class NotificationProvider : ProviderBase, IProvideNotifications
    {
        private readonly ILogger<NotificationProvider> _logger;

        public NotificationProvider(
            ILogger<NotificationProvider> logger,
            IHttpClientFactory clientFactory) :
            base(HttpClients.NotificationClient, clientFactory)
        {
            _logger = logger;
        }
    }
}
