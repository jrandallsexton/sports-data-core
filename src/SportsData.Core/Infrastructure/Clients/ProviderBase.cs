using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients
{
    public abstract class ProviderBase(
        string providerName,
        IHttpClientFactory clientFactory)
        : IProvideHealthChecks
    {
        protected readonly HttpClient HttpClient = clientFactory.CreateClient(providerName);

        public string GetProviderName()
        {
            return providerName;
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
                    { "host", hostName },
                    { "ex", ex.Message }
                };
            }
        }
    }
}
