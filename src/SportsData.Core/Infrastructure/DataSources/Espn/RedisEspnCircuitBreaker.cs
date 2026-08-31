using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using System;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.DataSources.Espn
{
    // TODO: This does not belong in Core
    public class RedisEspnCircuitBreaker : IEspnCircuitBreaker
    {
        /// <summary>
        /// Fixed, unprefixed key — the same namespace <see cref="RedisEspnRateLimiter"/>
        /// uses for <c>espn:ratelimit:bucket</c>.
        /// </summary>
        /// <remarks>
        /// Deliberately NOT read or written through IDistributedCache. That path applies
        /// the cache's InstanceName prefix, which is derived per application, so the ESPN
        /// circuit would silently land in an application-scoped namespace. The circuit is
        /// a GLOBAL concern — ESPN rate-limits by IP, so a circuit tripped by one caller
        /// must stop every caller on that address. Tying it to an application name means a
        /// rename, or a second service ever calling ESPN, quietly splits the circuit in two
        /// and the protection stops working with nothing to show for it.
        /// <para>
        /// Going direct through IConnectionMultiplexer also puts both ESPN mechanisms in
        /// one namespace, which is what makes splitting ESPN state onto its own Redis
        /// instance a config change rather than a refactor. Safe because Provider only
        /// registers this implementation when CacheServiceUri is set, which is precisely
        /// when AddCaching registers IConnectionMultiplexer; otherwise the NoOp
        /// implementations in Core are used.
        /// </para>
        /// </remarks>
        private const string CircuitKey = "espn:circuit:open";

        private readonly IConnectionMultiplexer _redis;
        private readonly IOptionsMonitor<EspnApiClientConfig> _configMonitor;
        private readonly ILogger<RedisEspnCircuitBreaker> _logger;

        public RedisEspnCircuitBreaker(
            IConnectionMultiplexer redis,
            IOptionsMonitor<EspnApiClientConfig> config,
            ILogger<RedisEspnCircuitBreaker> logger)
        {
            _redis = redis;
            _configMonitor = config;
            _logger = logger;
        }

        private IDatabase Db => _redis.GetDatabase();

        public async Task<bool> IsOpenAsync()
        {
            try
            {
                return await Db.KeyExistsAsync(CircuitKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check ESPN circuit breaker state for key {CircuitKey}", CircuitKey);
                return false; // Fail open — allow ESPN calls if Redis is unavailable
            }
        }

        public async Task TripAsync(string reason)
        {
            string? alreadyOpen = null;
            var readSucceeded = false;
            try
            {
                alreadyOpen = await Db.StringGetAsync(CircuitKey);
                readSucceeded = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read ESPN circuit breaker state for key {CircuitKey}", CircuitKey);
            }

            var cooldownSeconds = Math.Max(1, _configMonitor.CurrentValue.CircuitBreakerCooldownSeconds);
            var openUntil = DateTime.UtcNow.AddSeconds(cooldownSeconds);

            try
            {
                // Expiry as a relative TimeSpan rather than an absolute instant: Redis
                // takes a duration, and deriving it here keeps the key's lifetime tied to
                // the configured cooldown even if clocks disagree.
                await Db.StringSetAsync(
                    CircuitKey,
                    openUntil.ToString("O"),
                    TimeSpan.FromSeconds(cooldownSeconds));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist ESPN circuit breaker trip for key {CircuitKey}", CircuitKey);
            }

            if (readSucceeded && alreadyOpen is null)
            {
                _logger.LogCritical(
                    "ESPN circuit breaker TRIPPED. Reason: {Reason}. All ESPN API calls paused until {OpenUntil:u} ({CooldownSeconds}s cooldown)",
                    reason,
                    openUntil,
                    cooldownSeconds);
            }
        }

        public async Task<DateTime?> GetOpenUntilAsync()
        {
            try
            {
                var value = await Db.StringGetAsync(CircuitKey);
                if (!value.HasValue)
                    return null;

                return DateTime.TryParse(value.ToString(), out var dt) ? dt : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read ESPN circuit breaker state for key {CircuitKey}", CircuitKey);
                return null;
            }
        }
    }
}
