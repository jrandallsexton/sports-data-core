using Microsoft.Extensions.Diagnostics.HealthChecks;

using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Middleware.Health
{
    public class HealthCheck : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var providerName = context.Registration.Name;
            const bool isHealthy = true;

            return isHealthy ?
                await Task.FromResult(HealthCheckResult.Healthy($"{providerName} is healthy")) :
                await Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, $"{providerName} is unhealthy", null, null));
        }
    }
}
