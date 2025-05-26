using System.Collections.Generic;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Core.Infrastructure.Clients.Notification;
using SportsData.Core.Infrastructure.Clients.Player;
using SportsData.Core.Infrastructure.Clients.Producer;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Infrastructure.Clients.Venue;

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

        public Dictionary<Sport, ProviderConfig> ContestClientConfigs { get; set; }

        public Dictionary<Sport, ProviderConfig> FranchiseClientConfigs { get; set; }

        public Dictionary<Sport, ProviderConfig> NotificationClientConfigs { get; set; }

        public Dictionary<Sport, ProviderConfig> PlayerClientConfigs { get; set; }

        public Dictionary<Sport, ProviderConfig> ProducerClientConfigs { get; set; }

        public Dictionary<Sport, ProviderConfig> ProviderClientConfigs { get; set; }

        public Dictionary<Sport, ProviderConfig> SeasonClientConfigs { get; set; }

        public Dictionary<Sport, ProviderConfig> VenueClientConfigs { get; set; }

        public string FirebaseConfigJson { get; set; }

    }
}
