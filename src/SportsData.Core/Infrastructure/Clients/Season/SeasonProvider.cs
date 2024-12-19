using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Season
{
    public interface IProvideSeasons : IProvideHealthChecks
    {

    }

    public class SeasonProvider : IProvideSeasons
    {
        private readonly ILogger<SeasonProvider> _logger;
        private readonly HttpClient _httpClient;

        public SeasonProvider(
            ILogger<SeasonProvider> logger,
            IHttpClientFactory clientFactory)
        {
            _logger = logger;
            _httpClient = clientFactory.CreateClient(HttpClients.SeasonClient);
        }

        public string GetProviderName()
        {
            return HttpClients.SeasonClient;
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
