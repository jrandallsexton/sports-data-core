using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Franchise
{
    public interface IProvideFranchises : IProvideHealthChecks
    {

    }

    public class FranchiseProvider : IProvideFranchises
    {
        private readonly ILogger<FranchiseProvider> _logger;
        private readonly HttpClient _httpClient;

        public FranchiseProvider(
            ILogger<FranchiseProvider> logger,
            IHttpClientFactory clientFactory)
        {
            _logger = logger;
            _httpClient = clientFactory.CreateClient(HttpClients.FranchiseClient);
        }

        public string GetProviderName()
        {
            return HttpClients.FranchiseClient;
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
