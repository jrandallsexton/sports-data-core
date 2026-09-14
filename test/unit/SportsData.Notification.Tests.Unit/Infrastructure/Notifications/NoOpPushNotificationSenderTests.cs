using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SportsData.Core.Common;
using SportsData.Notification.Infrastructure.Notifications;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Infrastructure.Notifications;

/// <summary>
/// The reason handed to the no-op sender at registration is what operators
/// read in NotificationLog.FailureReason (consumers copy
/// failure.Errors.First().ErrorMessage straight into it). Pin that it
/// survives the round trip, so "push disabled by config" and "Firebase not
/// configured" stay distinguishable.
/// </summary>
public class NoOpPushNotificationSenderTests
{
    [Theory]
    [InlineData("push disabled by config (SportsData.Notification:NotificationConfig:PushEnabled is false)")]
    [InlineData("Firebase not configured (CommonConfig:Firebase:ProjectId is not set)")]
    public async Task SendAsync_ReturnsErrorFailure_CarryingTheRegisteredReason(string reason)
    {
        var sut = new NoOpPushNotificationSender(
            NullLogger<NoOpPushNotificationSender>.Instance,
            reason);

        var result = await sut.SendAsync("fcm-token-0123456789", "title", "body");

        var failure = result.Should().BeOfType<Failure<string>>().Subject;
        failure.Status.Should().Be(ResultStatus.Error);
        failure.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().StartWith(reason);
    }

    [Fact]
    public async Task SendAsync_NeverSucceeds_EvenForAWellFormedToken()
    {
        var sut = new NoOpPushNotificationSender(
            NullLogger<NoOpPushNotificationSender>.Instance,
            "any reason");

        var result = await sut.SendAsync("fcm-token-0123456789", "title", "body",
            new Dictionary<string, string> { ["k"] = "v" });

        result.Should().NotBeOfType<Success<string>>();
    }
}
