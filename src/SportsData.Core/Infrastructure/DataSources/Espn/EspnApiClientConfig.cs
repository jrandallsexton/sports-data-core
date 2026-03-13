namespace SportsData.Core.Infrastructure.DataSources.Espn
{
    public class EspnApiClientConfig
    {
        /// <summary>
        ///  try local disk first.
        /// </summary>
        public bool ReadFromCache { get; set; }

        /// <summary>
        /// bypass cache even if present.
        /// </summary>
        public bool ForceLiveFetch { get; set; }

        /// <summary>
        /// save ESPN responses to disk.
        /// </summary>
        public bool PersistLocally { get; set; }

        /// <summary>
        /// folder for disk persistence.
        /// </summary>
        public string LocalCacheDirectory { get; set; } = "./cache";

        /// <summary>
        /// Amount of delay between requests to ESPN API, in milliseconds.
        /// CRITICAL: ESPN uses IP-based rate limiting and returns 403 (not 429) when exceeded.
        /// Default of 1000ms (1 second) = 60 requests/minute.
        /// Previously increased from 250ms to 1000ms to avoid triggering ESPN rate limits.
        /// </summary>
        public int RequestDelayMs { get; set; } = 1000;

        /// <summary>
        /// Cooldown period (in seconds) after a 403 from ESPN before retrying.
        /// When the circuit breaker trips, ALL workers across ALL pods stop calling ESPN
        /// for this duration. Default 300s (5 minutes).
        /// </summary>
        public int CircuitBreakerCooldownSeconds { get; set; } = 300;

        /// <summary>
        /// Token bucket burst capacity. Small value prevents stampedes after idle periods.
        /// </summary>
        public int RateLimitMaxTokens { get; set; } = 2;

        /// <summary>
        /// Token refill rate. 1.0 = 1 request/second, matching the current 1000ms delay.
        /// </summary>
        public double RateLimitTokensPerSecond { get; set; } = 1.0;

        /// <summary>
        /// Maximum time (ms) a worker will block waiting for a token before failing open.
        /// </summary>
        public int RateLimitMaxWaitMs { get; set; } = 30000;
    }
}
