﻿using Microsoft.Extensions.Diagnostics.HealthChecks;

using SportsData.Core.Infrastructure.Clients.Player;

using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Middleware.Health
{
    public class HealthCheckPlayer : IHealthCheck
    {
        private readonly IProvidePlayers _provider;

        public HealthCheckPlayer(IProvidePlayers provider)
        {
            _provider = provider;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            var providerName = _provider.GetProviderName();
            var status = await _provider.GetHealthStatus();

            var isHealthy = status["status"].ToString() == "OK";

            return isHealthy ?
                HealthCheckResult.Healthy($"{providerName} is healthy") :
                new HealthCheckResult(context.Registration.FailureStatus, $"{providerName} is unhealthy", null, new ReadOnlyDictionary<string, object>(status));
        }
    }
}
