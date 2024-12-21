using System;
using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Player
{
    public interface IProvidePlayers : IProvideHealthChecks
    {

    }

    public class PlayerProvider : IProvidePlayers
    {
        private readonly ILogger<PlayerProvider> _logger;
        private readonly HttpClient _httpClient;

        public PlayerProvider(
            ILogger<PlayerProvider> logger,
            IHttpClientFactory clientFactory)
        {
            _logger = logger;
            _httpClient = clientFactory.CreateClient(HttpClients.PlayerClient);
        }

        public string GetProviderName()
        {
            return HttpClients.PlayerClient;
        }

        public async Task<Dictionary<string, object>> GetHealthStatus()
        {
            try
            {
                var response = await _httpClient.GetAsync("/health");
                var tmp = response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
                return new Dictionary<string, object>()
                {
                    { "status", response.StatusCode }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>()
                {
                    { "status", HttpStatusCode.ServiceUnavailable }
                };
            }
        }
    }
}
