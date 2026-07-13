using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Infrastructure.Data.Entities;
using SportsData.Notification.Infrastructure.Notifications;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Consumers;

// Covers the membership (welcome) push claiming/dispatching into the typed
// NotificationMembership table via the public Consume path.
public class PickemGroupMemberAddedConsumerTests : NotificationTestBase<PickemGroupMemberAddedConsumer>
{
    private static readonly DateTime FixedNow = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IPushNotificationSender> _pushSender;

    public PickemGroupMemberAddedConsumerTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);

        _pushSender = Mocker.GetMock<IPushNotificationSender>();
        _pushSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<string>("msg-id"));
    }

    private static ConsumeContext<PickemGroupMemberAdded> ContextFor(PickemGroupMemberAdded msg)
    {
        var ctx = new Mock<ConsumeContext<PickemGroupMemberAdded>>();
        ctx.SetupGet(x => x.Message).Returns(msg);
        ctx.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private static PickemGroupMemberAdded Msg(Guid userId, Guid groupId)
        => new(groupId, userId, Sport.FootballNcaa, 2026, Guid.NewGuid(), Guid.NewGuid());

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
        _pushSender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), times);

    [Fact]
    public async Task Consume_MemberWithDevice_Notifies()
    {
        var userId = Guid.NewGuid();
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<PickemGroupMemberAddedConsumer>();
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
            MembershipEnabled = false,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<PickemGroupMemberAddedConsumer>();
        await sut.Consume(ContextFor(Msg(userId, Guid.NewGuid())));

        VerifySendCount(Times.Never());
    }

    [Fact]
    public async Task Consume_NoEnabledDevice_DoesNotNotify()
    {
        var userId = Guid.NewGuid();
        await SeedDeviceAsync(userId, enabled: false);

        var sut = Mocker.CreateInstance<PickemGroupMemberAddedConsumer>();
        await sut.Consume(ContextFor(Msg(userId, Guid.NewGuid())));

        VerifySendCount(Times.Never());
    }

    [Fact]
    public async Task Consume_PersistsTypedRow_WithMembershipMetadata()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        await SeedDeviceAsync(userId);

        var msg = Msg(userId, groupId);
        var sut = Mocker.CreateInstance<PickemGroupMemberAddedConsumer>();
        await sut.Consume(ContextFor(msg));

        var row = await DataContext.NotificationMemberships.SingleAsync();
        row.UserId.Should().Be(userId);
        row.LeagueId.Should().Be(groupId);
        row.CorrelationId.Should().Be(msg.CorrelationId);
        row.Channel.Should().Be("Fcm");
        row.Result.Should().Be("Sent");
    }
}
