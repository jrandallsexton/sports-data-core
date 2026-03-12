using System;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.DataSources.Espn
{
    public interface IEspnCircuitBreaker
    {
        /// <summary>
        /// Returns true if the circuit is open (ESPN is rate-limiting).
        /// </summary>
        Task<bool> IsOpenAsync();

        /// <summary>
        /// Trips the circuit, preventing further ESPN calls for the configured cooldown period.
        /// </summary>
        Task TripAsync(string reason);

        /// <summary>
        /// Returns the UTC time the circuit will close, or null if already closed.
        /// </summary>
        Task<DateTime?> GetOpenUntilAsync();
    }
}
