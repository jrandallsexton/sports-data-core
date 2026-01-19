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

        public LoggingHealthCheck(IOptions<CommonConfig> config)
        {
            _config = config;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken)
        {
            var response = await new HttpClient().GetAsync(_config.Value.Logging.SeqUri, cancellationToken);

            return response.IsSuccessStatusCode ?
                HealthCheckResult.Healthy() :
                HealthCheckResult.Unhealthy("Cannot connect to Seq");
        }
    }
}