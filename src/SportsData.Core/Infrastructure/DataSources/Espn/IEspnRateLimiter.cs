using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.DataSources.Espn
{
    public interface IEspnRateLimiter
    {
        /// <summary>
        /// Acquires a token from the rate limiter. Blocks until a token is available
        /// or the max wait time is exceeded. Always returns true (fail-open).
        /// </summary>
        Task<bool> AcquireAsync(CancellationToken ct = default);
    }
}
