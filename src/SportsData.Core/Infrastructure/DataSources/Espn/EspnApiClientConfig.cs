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
        /// </summary>
        public int RequestDelayMs { get; set; } = 1000;
    }
}
