using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
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
            try
            {
                var response = await HttpClient.GetAsync("health");
                var tmp = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
                return new Dictionary<string, object>()
                {
                    { "status", response.StatusCode },
                    { "uri",  $"{HttpClient.BaseAddress}health" },
                    { "host", Environment.MachineName }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>()
                {
                    { "status", HttpStatusCode.ServiceUnavailable },
                    { "uri",  $"{HttpClient.BaseAddress}health" },
                    { "host", Environment.MachineName }
                };
            }
        }
    }
}
