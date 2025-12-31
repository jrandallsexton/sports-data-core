namespace SportsData.Api.Infrastructure.Notifications
{
    public class NotificationConfig
    {
        public EmailConfig Email { get; set; } = null!;

        public class EmailConfig
        {
            public required string ApiKey { get; set; }

            public required string FromEmail { get; set; }

            public required string TemplateIdInvitation { get; set; }

            public required string UrlBase { get; set; }
        }

        public class SmsConfig
        {

        }
    }
}
