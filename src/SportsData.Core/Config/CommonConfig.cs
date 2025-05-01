using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Core.Infrastructure.Clients.Notification;
using SportsData.Core.Infrastructure.Clients.Player;
using SportsData.Core.Infrastructure.Clients.Producer;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Infrastructure.Clients.Venue;

using System.Collections.Generic;

using static SportsData.Core.Config.CommonConfig;

namespace SportsData.Core.Config
{
    public class CommonConfig
    {
        public required string AzureBlobStorageConnectionString { get; set; }

        public required string AzureBlobStorageUrl { get; set; }

        public required string AzureBlobStorageContainerPrefix { get; set; }

        public required string AzureServiceBusConnectionString { get; set; }

        public required string SeqUri { get; set; }

        public required string RedisUri { get; set; }

        public List<ProviderConfig> ProviderConfigs { get; set; }

        public FirebaseConfiguration FirebaseConfig { get; set; }

        public string FirebaseConfigJson { get; set; }

        public class ProviderConfig
        {
            public string Name { get; set; }
            public string SecretKey { get; set; }
            public string ApiUrl { get; set; }
        }

        public class FirebaseConfiguration
        {
            public string Type { get; set; }
            public string ProjectId { get; set; }
            public string PrivateKeyId { get; set; }
            public string PrivateKey { get; set; }
            public string ClientEmail { get; set; }
            public string ClientId { get; set; }
            public string AuthUri { get; set; }
            public string TokenUri { get; set; }
            public string AuthProviderX509CertUrl { get; set; }
            public string ClientX509CertUrl { get; set; }
            public string UniverseDomain { get; set; }
        }
    }

    public static class CommonConfigKeys
    {
        public static string FirebaseConfigType =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.Type)}";
        public static string FirebaseConfigProjectId =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.ProjectId)}";
        public static string FirebaseConfigPrivateKeyId =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.PrivateKeyId)}";
        public static string FirebaseConfigPrivateKey =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.PrivateKey)}";
        public static string FirebaseConfigClientEmail =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.ClientEmail)}";
        public static string FirebaseConfigClientId =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.ClientId)}";
        public static string FirebaseConfigAuthUri =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.AuthUri)}";
        public static string FirebaseConfigTokenUri =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.TokenUri)}";
        public static string FirebaseConfigAuthProviderX509CertUrl =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.AuthProviderX509CertUrl)}";
        public static string FirebaseConfigClientX509CertUrl =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.ClientX509CertUrl)}";
        public static string FirebaseConfigUniverseDomain =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.UniverseDomain)}";

        public static string AzureBlobStorage =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.AzureBlobStorageConnectionString)}";

        public static string AzureBlobStorageUrl =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.AzureBlobStorageUrl)}";

        public static string AzureBlobStorageContainerPrefix =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.AzureBlobStorageContainerPrefix)}";

        public static string AzureServiceBus =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.AzureServiceBusConnectionString)}";

        public static string CacheServiceUri =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.RedisUri)}";

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

        public static string ProviderProviderUri =>
            $"{nameof(CommonConfig)}:{nameof(ProviderProviderConfig)}:{nameof(ProviderProviderConfig.ApiUrl)}";

        public static string SeasonProviderUri =>
            $"{nameof(CommonConfig)}:{nameof(SeasonProviderConfig)}:{nameof(SeasonProviderConfig.ApiUrl)}";

        public static string VenueProviderUri =>
            $"{nameof(CommonConfig)}:{nameof(VenueProviderConfig)}:{nameof(VenueProviderConfig.ApiUrl)}";
    }
}
