using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
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
            var kvp = new Dictionary<string, object>()
            {
                {"host", Environment.MachineName},
                {"myName", Assembly.GetEntryAssembly().FullName}
            };

            const bool isHealthy = true;

            return isHealthy ?
                await Task.FromResult(HealthCheckResult.Healthy($"{providerName} is healthy", new ReadOnlyDictionary<string, object>(kvp))) :
                await Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, $"{providerName} is unhealthy", null, null));
        }
    }
}
