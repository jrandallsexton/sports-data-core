using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Contest
{
    public interface IProvideContests : IProvideHealthChecks
    {

    }

    public class ContestProvider : IProvideContests
    {
        private readonly ILogger<ContestProvider> _logger;
        private readonly HttpClient _httpClient;

        public ContestProvider(
            ILogger<ContestProvider> logger,
            IHttpClientFactory clientFactory)
        {
            _logger = logger;
            _httpClient = clientFactory.CreateClient(HttpClients.ContestClient);
        }

        public string GetProviderName()
        {
            return HttpClients.ContestClient;
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
