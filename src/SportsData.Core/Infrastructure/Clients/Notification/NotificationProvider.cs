using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Notification
{
    public interface IProvideNotifications : IProvideHealthChecks
    {

    }

    public class NotificationProvider : IProvideNotifications
    {
        private readonly ILogger<NotificationProvider> _logger;
        private readonly HttpClient _httpClient;

        public NotificationProvider(
            ILogger<NotificationProvider> logger,
            IHttpClientFactory clientFactory)
        {
            _logger = logger;
            _httpClient = clientFactory.CreateClient(HttpClients.NotificationClient);
        }
        public string GetProviderName()
        {
            return HttpClients.NotificationClient;
        }

        public async Task<Dictionary<string, object>> GetHealthStatus()
        {
            // TODO: Make this better by using the actual result. Determine a pattern.
            var response = await _httpClient.GetAsync("/health");
            var tmp = response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            return new Dictionary<string, object>()
            {
                { "status", response.StatusCode }
            };
        }
    }
}
