namespace SportsData.Provider.Config
{
    public class ProviderWorkerConfig
    {
        /// <summary>
        /// When true, Provider will not register the DocumentRequestedHandler consumer.
        /// Messages remain in the queue untouched. Publishing and Hangfire job processing continue.
        /// Requires pod restart to take effect.
        /// </summary>
        public bool PauseMessageConsumption { get; set; }
    }
}
