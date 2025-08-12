//using Microsoft.Extensions.Logging;

//using SportsData.Core.Middleware.Health;

//using System.Net.Http;

//namespace SportsData.Core.Infrastructure.Clients.Notification
//{
//    public interface IProvideNotifications : IProvideHealthChecks
//    {

//    }

//    public class NotificationClient : ClientBase, IProvideNotifications
//    {
//        private readonly ILogger<NotificationClient> _logger;

//        public NotificationClient(
//            ILogger<NotificationClient> logger,
//            IHttpClientFactory clientFactory) :
//            base(HttpClients.NotificationClient, clientFactory)
//        {
//            _logger = logger;
//        }
//    }
//}
