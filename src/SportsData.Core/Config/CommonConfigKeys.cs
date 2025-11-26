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
    public static class CommonConfigKeys
    {
        //public static string FirebaseConfigType =>
        //    $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.Type)}";
        //public static string FirebaseConfigProjectId =>
        //    $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.ProjectId)}";
        //public static string FirebaseConfigPrivateKeyId =>
        //    $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.PrivateKeyId)}";
        //public static string FirebaseConfigPrivateKey =>
        //    $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.PrivateKey)}";
        //public static string FirebaseConfigClientEmail =>
        //    $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.ClientEmail)}";
        //public static string FirebaseConfigClientId =>
        //    $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.ClientId)}";
        //public static string FirebaseConfigAuthUri =>
        //    $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.AuthUri)}";
        //public static string FirebaseConfigTokenUri =>
        //    $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.TokenUri)}";
        //public static string FirebaseConfigAuthProviderX509CertUrl =>
        //    $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.AuthProviderX509CertUrl)}";
        //public static string FirebaseConfigClientX509CertUrl =>
        //    $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.ClientX509CertUrl)}";
        //public static string FirebaseConfigUniverseDomain =>
        //    $"{nameof(CommonConfig)}:{nameof(CommonConfig.FirebaseConfig)}:{nameof(FirebaseConfiguration.UniverseDomain)}";

        public static string AzureBlobStorage =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.AzureBlobStorageConnectionString)}";

        public static string AzureBlobStorageUrl =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.AzureBlobStorageUrl)}";

        public static string AzureServiceBus =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.AzureServiceBusConnectionString)}";

        public static string CacheServiceUri =>
            $"{nameof(CommonConfig)}:{nameof(CommonConfig.RedisUri)}";

        public static string GetContestProviderUri(Sport mode) =>
            $"{nameof(CommonConfig)}:{nameof(ContestClientConfig)}:{mode}:{nameof(ContestClientConfig.ApiUrl)}";

        public static string GetFranchiseProviderUri(Sport mode) =>
            $"{nameof(CommonConfig)}:{nameof(FranchiseClientConfig)}:{mode}:{nameof(FranchiseClientConfig.ApiUrl)}";

        public static string GetNotificationProviderUri(Sport mode) =>
            $"{nameof(CommonConfig)}:{nameof(NotificationClientConfig)}:{mode}:{nameof(NotificationClientConfig.ApiUrl)}";

        public static string GetPlayerProviderUri(Sport mode) =>
            $"{nameof(CommonConfig)}:{nameof(PlayerClientConfig)}:{mode}:{nameof(PlayerClientConfig.ApiUrl)}";

        public static string GetProducerProviderUri() =>
            $"{nameof(CommonConfig)}:{nameof(ProducerClientConfig)}:{nameof(ProducerClientConfig.ApiUrl)}";

        public static string GetProviderProviderUri() =>
            $"{nameof(CommonConfig)}:{nameof(ProviderClientConfig)}:{nameof(ProviderClientConfig.ApiUrl)}";

        public static string GetSeasonProviderUri(Sport mode) =>
            $"{nameof(CommonConfig)}:{nameof(SeasonClientConfig)}:{mode}:{nameof(SeasonClientConfig.ApiUrl)}";

        public static string GetVenueProviderUri() =>
            $"{nameof(CommonConfig)}:{nameof(VenueClientConfig)}:{nameof(VenueClientConfig.ApiUrl)}";
    }
}
