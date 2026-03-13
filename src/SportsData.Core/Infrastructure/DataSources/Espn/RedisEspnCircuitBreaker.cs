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
        private readonly IOptionsMonitor<EspnApiClientConfig> _configMonitor;
        private readonly ILogger<RedisEspnCircuitBreaker> _logger;

        public RedisEspnCircuitBreaker(
            IDistributedCache cache,
            IOptionsMonitor<EspnApiClientConfig> config,
            ILogger<RedisEspnCircuitBreaker> logger)
        {
            _cache = cache;
            _configMonitor = config;
            _logger = logger;
        }

        public async Task<bool> IsOpenAsync()
        {
            try
            {
                var value = await _cache.GetStringAsync(CircuitKey);
                return value is not null;
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
                alreadyOpen = await _cache.GetStringAsync(CircuitKey);
                readSucceeded = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read ESPN circuit breaker state for key {CircuitKey}", CircuitKey);
            }

            var openUntil = DateTime.UtcNow.AddSeconds(_configMonitor.CurrentValue.CircuitBreakerCooldownSeconds);

            try
            {
                await _cache.SetStringAsync(CircuitKey, openUntil.ToString("O"), new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = openUntil
                });
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
                    _configMonitor.CurrentValue.CircuitBreakerCooldownSeconds);
            }
        }

        public async Task<DateTime?> GetOpenUntilAsync()
        {
            try
            {
                var value = await _cache.GetStringAsync(CircuitKey);
                if (value is null)
                    return null;

                return DateTime.TryParse(value, out var dt) ? dt : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read ESPN circuit breaker state for key {CircuitKey}", CircuitKey);
                return null;
            }
        }
    }
}
