using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Producer
{
    public interface IProvideProducers : IProvideHealthChecks
    {

    }

    public class ProducerProvider : IProvideProducers
    {
        private readonly ILogger<ProducerProvider> _logger;
        private readonly HttpClient _httpClient;

        public ProducerProvider(
            ILogger<ProducerProvider> logger,
            IHttpClientFactory clientFactory)
        {
            _logger = logger;
            _httpClient = clientFactory.CreateClient(HttpClients.ContestClient);
        }

        public string GetProviderName()
        {
            return HttpClients.ProducerClient;
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
                    { "status", response.StatusCode },
                    { "uri",  $"{_httpClient.BaseAddress}/health" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>()
                {
                    { "status", HttpStatusCode.ServiceUnavailable },
                    { "uri",  $"{_httpClient.BaseAddress}/health" }
                };
            }
        }
    }
}
