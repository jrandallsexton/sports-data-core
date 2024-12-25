using System;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Middleware.Health
{
    public class ProviderHealthCheck<T> : IHealthCheck where T : IProvideHealthChecks
    {
        private readonly T _provider;

        public ProviderHealthCheck(T provider)
        {
            _provider = provider;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            var providerName = _provider.GetProviderName();
            var status = await _provider.GetHealthStatus();

            var isHealthy = status["status"].ToString() == "OK";

            return isHealthy ?
                HealthCheckResult.Healthy($"{providerName} is healthy on {Environment.MachineName}", new ReadOnlyDictionary<string, object>(status)) :
                new HealthCheckResult(context.Registration.FailureStatus, $"{providerName} is unhealthy", null, new ReadOnlyDictionary<string, object>(status));
        }
    }
}
