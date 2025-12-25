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
    }
}
