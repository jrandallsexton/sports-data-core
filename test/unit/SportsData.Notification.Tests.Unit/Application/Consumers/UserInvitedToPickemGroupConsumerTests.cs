using FluentAssertions;

using MassTransit;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Infrastructure.Data.Entities;
using SportsData.Notification.Infrastructure.Notifications;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Consumers;

public class UserInvitedToPickemGroupConsumerTests : NotificationTestBase<UserInvitedToPickemGroupConsumer>
{
    private static readonly DateTime FixedNow = new(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IPushNotificationSender> _pushSender;

    public UserInvitedToPickemGroupConsumerTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);

        _pushSender = Mocker.GetMock<IPushNotificationSender>();
        _pushSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<string>("msg-id"));
    }

    private static ConsumeContext<UserInvitedToPickemGroup> ContextFor(UserInvitedToPickemGroup msg)
    {
        var ctx = new Mock<ConsumeContext<UserInvitedToPickemGroup>>();
        ctx.SetupGet(x => x.Message).Returns(msg);
        ctx.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private static UserInvitedToPickemGroup Msg(Guid inviteeUserId, Guid groupId)
        => new(inviteeUserId, groupId, "Saturday Smackdown", Guid.NewGuid(),
            Sport.FootballNcaa, 2026, Guid.NewGuid(), Guid.NewGuid());

    private async Task SeedDeviceAsync(Guid userId, bool enabled = true)
    {
        DataContext.UserDevices.Add(new UserDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            InstallationId = Guid.NewGuid().ToString(),
            FcmToken = "tok",
            Platform = "ios",
            NotificationsEnabled = enabled,
            LastSeenUtc = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    private void VerifySendCount(Times times) =>
        _pushSender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task Consume_RegisteredInviteeWithDevice_Notifies()
    {
        var userId = Guid.NewGuid();
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<UserInvitedToPickemGroupConsumer>();
        await sut.Consume(ContextFor(Msg(userId, Guid.NewGuid())));

        VerifySendCount(Times.Once());
    }

    [Fact]
    public async Task Consume_OptedOut_DoesNotNotify()
    {
        var userId = Guid.NewGuid();
        await SeedDeviceAsync(userId);
        DataContext.UserNotificationPreferences.Add(new UserNotificationPreferences
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LeagueInviteEnabled = false,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<UserInvitedToPickemGroupConsumer>();
        await sut.Consume(ContextFor(Msg(userId, Guid.NewGuid())));

        VerifySendCount(Times.Never());
    }

    [Fact]
    public async Task Consume_NoEnabledDevice_DoesNotNotify()
    {
        var userId = Guid.NewGuid();
        await SeedDeviceAsync(userId, enabled: false);

        var sut = Mocker.CreateInstance<UserInvitedToPickemGroupConsumer>();
        await sut.Consume(ContextFor(Msg(userId, Guid.NewGuid())));

        VerifySendCount(Times.Never());
    }

    [Fact]
    public async Task Consume_PopulatesDeepLinkDataPayload()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        await SeedDeviceAsync(userId);

        IReadOnlyDictionary<string, string> captured = null;
        _pushSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IReadOnlyDictionary<string, string>, CancellationToken>(
                (_, _, _, data, _) => captured = data)
            .ReturnsAsync(new Success<string>("msg-id"));

        var sut = Mocker.CreateInstance<UserInvitedToPickemGroupConsumer>();
        await sut.Consume(ContextFor(Msg(userId, groupId)));

        captured.Should().NotBeNull();
        captured["kind"].Should().Be("LeagueInvite");
        captured["target"].Should().Be("invite-preview");
        captured["leagueId"].Should().Be(groupId.ToString());
    }
}
