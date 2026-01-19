using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using SportsData.Core.Config;

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Middleware.Health
{
    public class LoggingHealthCheck : IHealthCheck
    {
        private readonly IOptions<CommonConfig> _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public LoggingHealthCheck(IOptions<CommonConfig> config, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient("Seq");
            var response = await httpClient.GetAsync(_config.Value.Logging.SeqUri, cancellationToken);

            return response.IsSuccessStatusCode ?
                HealthCheckResult.Healthy() :
                HealthCheckResult.Unhealthy("Cannot connect to Seq");
        }
    }
}