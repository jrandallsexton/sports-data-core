using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SportsData.Provider.Middleware
{
    public class HealthCheckApi : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            const string providerName = "sports-data-provider";
            var isHealthy = true;
            return isHealthy ?
                await Task.FromResult(HealthCheckResult.Healthy($"{providerName} is healthy")) :
                await Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, $"{providerName} is unhealthy", null, null));
        }
    }
}
