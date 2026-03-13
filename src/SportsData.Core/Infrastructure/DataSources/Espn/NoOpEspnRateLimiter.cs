using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.DataSources.Espn
{
    /// <summary>
    /// Default rate limiter that immediately allows all requests.
    /// Used by services that don't need centralized ESPN rate limiting.
    /// </summary>
    public class NoOpEspnRateLimiter : IEspnRateLimiter
    {
        public Task<bool> AcquireAsync(CancellationToken ct = default) => Task.FromResult(true);
    }
}
