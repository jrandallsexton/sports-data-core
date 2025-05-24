using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Middleware.Health
{
    public class DatabaseHealthCheck<T>(T dbContext, ILogger<DatabaseHealthCheck<T>> logger) : IHealthCheck where T : DbContext
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = new())
        {
            try
            {
                var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

                return canConnect ?
                    HealthCheckResult.Healthy() :
                    HealthCheckResult.Unhealthy($"Unable to connect to: {dbContext.Database?.ProviderName}");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"Unable to connect to: {dbContext.Database?.ProviderName}", ex);
            }
        }
    }
}
