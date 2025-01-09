using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using SportsData.Core.Extensions;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Middleware.Health
{
    public class CachingHealthCheck : IHealthCheck
    {
        private readonly ILogger<CachingHealthCheck> _logger;

        private readonly IDistributedCache _cache;

        public CachingHealthCheck(ILogger<CachingHealthCheck> logger, IDistributedCache cache)
        {
            _logger = logger;
            _cache = cache;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            try
            {
                await _cache.SetRecordAsync(nameof(CachingHealthCheck), "Test");
                var result = await _cache.GetRecordAsync<string>(nameof(CachingHealthCheck));
                var matches = result == "Test";

                return matches ?
                    HealthCheckResult.Healthy() :
                    HealthCheckResult.Unhealthy($"Unable to connect to cache service");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"Unable to connect to cache service", ex);
            }
        }
    }
}
