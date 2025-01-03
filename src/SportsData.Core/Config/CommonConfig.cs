using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Core.Infrastructure.Clients.Notification;
using SportsData.Core.Infrastructure.Clients.Player;
using SportsData.Core.Infrastructure.Clients.Producer;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Infrastructure.Clients.Venue;

using System.Collections.Generic;

namespace SportsData.Core.Config
{
    public class CommonConfig
    {
        public string AzureServiceBusConnectionString { get; set; }

        public string SeqUri { get; set; }

        public List<ProviderConfig> ProviderConfigs { get; set; }

        public class ProviderConfig
        {
            public string Name { get; set; }
            public string SecretKey { get; set; }
            public string ApiUrl { get; set; }
        }
    }

    public static class CommonConfigKeys
    {
        public static string AzureServiceBus =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.AzureServiceBusConnectionString)}";

        public static string ContestProviderUri =>
            $"{nameof(CommonConfig)}:{nameof(ContestProviderConfig)}:{nameof(ContestProviderConfig.ApiUrl)}";

        public static string FranchiseProviderUri =>
            $"{nameof(CommonConfig)}:{nameof(FranchiseProviderConfig)}:{nameof(FranchiseProviderConfig.ApiUrl)}";

        public static string NotificationProviderUri =>
            $"{nameof(CommonConfig)}:{nameof(NotificationProviderConfig)}:{nameof(NotificationProviderConfig.ApiUrl)}";

        public static string PlayerProviderUri =>
            $"{nameof(CommonConfig)}:{nameof(PlayerProviderConfig)}:{nameof(PlayerProviderConfig.ApiUrl)}";

        public static string ProducerProviderUri =>
            $"{nameof(CommonConfig)}:{nameof(ProducerProviderConfig)}:{nameof(ProducerProviderConfig.ApiUrl)}";

        public static string SeasonProviderUri =>
            $"{nameof(CommonConfig)}:{nameof(SeasonProviderConfig)}:{nameof(SeasonProviderConfig.ApiUrl)}";

        public static string VenueProviderUri =>
            $"{nameof(CommonConfig)}:{nameof(VenueProviderConfig)}:{nameof(VenueProviderConfig.ApiUrl)}";
    }
}
