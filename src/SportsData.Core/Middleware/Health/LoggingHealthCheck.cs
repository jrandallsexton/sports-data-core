using System.Net.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Middleware.Health
{
    public class LoggingHealthCheck : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            var response = await new HttpClient().GetAsync("http://seq-ui/#/events?range=1d", cancellationToken);
            
            return response.IsSuccessStatusCode ?
                HealthCheckResult.Healthy() :
                HealthCheckResult.Unhealthy("Cannot connect to Seq");
        }
    }
}