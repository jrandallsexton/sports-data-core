using FluentAssertions;
using Microsoft.Extensions.Options;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Api.Tests.Integration.Infrastructure.Notifications
{
    public class NotificationServiceTests : IntegrationTestBase<NotificationService>
    {
        [Fact(Skip="debugging purposes only")]
        public async Task WhenValid_EmailShouldSend()
        {
            // arrange
            var email = new NotificationService(null);

            // act
            var response = await email.SendEmailAsync(
                "authorized_email_address_here",
                "campaign_id_here",
                new
                {
                    firstName = "name_here",
                    leagueName = "league_name",
                    joinUrl = "https://sportdeets.com/join/abc123"
                });

            // assert
            response.Should().NotBeNull();
        }

        [Fact(Skip = "debugging purposes only")]
        public async Task WhenValid_SmsShouldSend()
        {
            var config = new NotificationConfig()
            {
                Email = new NotificationConfig.EmailConfig()
                {
                    ApiKey = "foo",
                    FromEmail = "bar",
                    TemplateIdInvitation = "none"
                }
            };
            var options = Options.Create(config);

            // arrange
            var sms = new NotificationService(options);

            // act
            var response = await sms.SendSmsAsync("10_digit_number_here", "sms_msg_here");

            // assert
            response.Should().NotBeNull();
        }
    }
}
