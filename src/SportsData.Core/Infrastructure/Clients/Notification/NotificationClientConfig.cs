namespace SportsData.Core.Infrastructure.Clients.Notification
{
    public class NotificationClientConfig
    {
        public required string ApiUrl { get; set; }

        /// <summary>
        /// The X-Api-Key value Notification's [ApiKeyAuth] admin endpoints
        /// require — same secret as CommonConfig:Notification:AdminApiKey on
        /// the Notification side. Stamped as a default header at client
        /// registration so call sites never handle it.
        /// </summary>
        public string? SecretKey { get; set; }
    }
}
