using SportsData.Core.Common;

using System.Collections.Generic;

namespace SportsData.Core.Config
{
    public class CommonConfig
    {
        public required string AzureBlobStorageConnectionString { get; set; }

        public required string AzureBlobStorageUrl { get; set; }

        public required string AzureBlobStorageContainerPrefix { get; set; }

        public required string AzureServiceBusConnectionString { get; set; }

        public required string SqlBaseConnectionString { get; set; }

        public required string SeqUri { get; set; }

        public required string RedisUri { get; set; }

        public required Dictionary<Sport, ProviderConfig> ContestClientConfigs { get; set; }

        public required Dictionary<Sport, ProviderConfig> FranchiseClientConfigs { get; set; }

        public required Dictionary<Sport, ProviderConfig> NotificationClientConfigs { get; set; }

        public required Dictionary<Sport, ProviderConfig> PlayerClientConfigs { get; set; }

        public required Dictionary<Sport, ProviderConfig> ProducerClientConfigs { get; set; }

        public required Dictionary<Sport, ProviderConfig> ProviderClientConfigs { get; set; }

        public required Dictionary<Sport, ProviderConfig> SeasonClientConfigs { get; set; }

        public required Dictionary<Sport, ProviderConfig> VenueClientConfigs { get; set; }

        public required string FirebaseConfigJson { get; set; }

        public LoggingConfig Logging { get; set; } = new();


        public class LoggingConfig
        {
            public string MinimumLevel { get; set; } = "Information";
            public Dictionary<string, string> Overrides { get; set; } = new();
            public string SeqMinimumLevel { get; set; } = "Information";
        }

    }
}
