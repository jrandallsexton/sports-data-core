using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.DataSources.Espn
{
    public class RedisEspnCircuitBreaker : IEspnCircuitBreaker
    {
        private const string CircuitKey = "espn:circuit:open";

        private readonly IDistributedCache _cache;
        private readonly ILogger<RedisEspnCircuitBreaker> _logger;
        private readonly int _cooldownSeconds;

        public RedisEspnCircuitBreaker(
            IDistributedCache cache,
            IOptions<EspnApiClientConfig> config,
            ILogger<RedisEspnCircuitBreaker> logger)
        {
            _cache = cache;
            _logger = logger;
            _cooldownSeconds = config.Value.CircuitBreakerCooldownSeconds;
        }

        public async Task<bool> IsOpenAsync()
        {
            var value = await _cache.GetStringAsync(CircuitKey);
            return value is not null;
        }

        public async Task TripAsync(string reason)
        {
            // Only log if the circuit isn't already open (avoid spamming on every 403)
            var alreadyOpen = await _cache.GetStringAsync(CircuitKey);

            var openUntil = DateTime.UtcNow.AddSeconds(_cooldownSeconds);

            await _cache.SetStringAsync(CircuitKey, openUntil.ToString("O"), new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = openUntil
            });

            if (alreadyOpen is null)
            {
                _logger.LogCritical(
                    "ESPN circuit breaker TRIPPED. Reason: {Reason}. All ESPN API calls paused until {OpenUntil:u} ({CooldownSeconds}s cooldown)",
                    reason,
                    openUntil,
                    _cooldownSeconds);
            }
        }

        public async Task<DateTime?> GetOpenUntilAsync()
        {
            var value = await _cache.GetStringAsync(CircuitKey);
            if (value is null)
                return null;

            return DateTime.TryParse(value, out var dt) ? dt : null;
        }
    }
}
