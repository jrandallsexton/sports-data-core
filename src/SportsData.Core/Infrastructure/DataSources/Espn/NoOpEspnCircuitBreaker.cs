using System;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.DataSources.Espn
{
    /// <summary>
    /// Default circuit breaker that is always closed (never blocks ESPN calls).
    /// Used by services that don't have Redis configured.
    /// </summary>
    public class NoOpEspnCircuitBreaker : IEspnCircuitBreaker
    {
        public Task<bool> IsOpenAsync() => Task.FromResult(false);

        public Task TripAsync(string reason) => Task.CompletedTask;

        public Task<DateTime?> GetOpenUntilAsync() => Task.FromResult<DateTime?>(null);
    }
}
