using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients
{
    public abstract class ProviderBase : IProvideHealthChecks
    {
        private readonly string _providerName;
        protected readonly HttpClient HttpClient;

        public ProviderBase(string providerName,
            IHttpClientFactory clientFactory)
        {
            _providerName = providerName;
            HttpClient = clientFactory.CreateClient(providerName);
        }

        public string GetProviderName()
        {
            return _providerName;
        }

        public async Task<Dictionary<string, object>> GetHealthStatus()
        {

            var hostName = string.Empty;

            try
            {
                var response = await HttpClient.GetAsync("health");
                response.EnsureSuccessStatusCode();

                if (response.Headers.TryGetValues("host", out var values))
                {
                    hostName = values.First();
                }

                return new Dictionary<string, object>()
                {
                    { "status", response.StatusCode },
                    { "uri",  $"{HttpClient.BaseAddress}health" },
                    { "host", hostName }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>()
                {
                    { "status", HttpStatusCode.ServiceUnavailable },
                    { "uri",  $"{HttpClient.BaseAddress}health" },
                    { "host", hostName }
                };
            }
        }
    }
}
