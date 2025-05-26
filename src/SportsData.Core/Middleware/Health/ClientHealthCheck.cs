using Microsoft.Extensions.Diagnostics.HealthChecks;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Middleware.Health
{
    public class ClientHealthCheck<T> : IHealthCheck where T : IProvideHealthChecks
    {
        private readonly T _provider;

        public ClientHealthCheck(T provider)
        {
            _provider = provider;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = new CancellationToken())
        {
            var providerName = _provider.GetProviderName();

            try
            {
                var status = await _provider.GetHealthStatus();

                var isHealthy = status["status"].ToString() == "OK";

                return isHealthy ?
                    HealthCheckResult.Healthy($"{providerName} is healthy", new ReadOnlyDictionary<string, object>(status)) :
                    new HealthCheckResult(context.Registration.FailureStatus, $"{providerName} is unhealthy", null, new ReadOnlyDictionary<string, object>(status));
            }
            catch (Exception ex)
            {
                var error = new Dictionary<string, object> { { "Exception", ex.ToString() } };
                return new HealthCheckResult(context.Registration.FailureStatus, $"{providerName} is unhealthy", null, new ReadOnlyDictionary<string, object>(error));
            }

        }
    }
}
