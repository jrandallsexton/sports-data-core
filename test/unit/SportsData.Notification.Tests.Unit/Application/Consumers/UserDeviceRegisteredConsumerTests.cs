using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Users;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Consumers;

public class UserDeviceRegisteredConsumerTests : NotificationTestBase<UserDeviceRegisteredConsumer>
{
    private static readonly DateTime FixedNow = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    public UserDeviceRegisteredConsumerTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);
    }

    private static ConsumeContext<UserDeviceRegistered> ContextFor(UserDeviceRegistered msg)
    {
        var ctx = new Mock<ConsumeContext<UserDeviceRegistered>>();
        ctx.SetupGet(x => x.Message).Returns(msg);
        ctx.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private static UserDeviceRegistered Msg(Guid userId, string installationId, string token = "token-1", string platform = "ios")
        => new(userId, installationId, token, platform, Guid.NewGuid(), Guid.NewGuid());

    private async Task SeedDeviceAsync(Guid userId, string installationId, string token, bool notificationsEnabled)
    {
        DataContext.UserDevices.Add(new UserDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            InstallationId = installationId,
            FcmToken = token,
            Platform = "ios",
            NotificationsEnabled = notificationsEnabled,
            LastSeenUtc = FixedNow.AddDays(-1),
            CreatedUtc = FixedNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Consume_InsertsNewRow_WhenInstallationUnknown()
    {
        var userId = Guid.NewGuid();
        var sut = Mocker.CreateInstance<UserDeviceRegisteredConsumer>();

        await sut.Consume(ContextFor(Msg(userId, "install-A", "tok-A")));

        var row = await DataContext.UserDevices.SingleAsync();
        row.UserId.Should().Be(userId);
        row.InstallationId.Should().Be("install-A");
        row.FcmToken.Should().Be("tok-A");
        row.NotificationsEnabled.Should().BeTrue();
        row.LastSeenUtc.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Consume_SameOwnerReRegister_PreservesOptOut_AndUpdatesToken()
    {
        var userId = Guid.NewGuid();
        await SeedDeviceAsync(userId, "install-A", "old-token", notificationsEnabled: false);

        var sut = Mocker.CreateInstance<UserDeviceRegisteredConsumer>();
        await sut.Consume(ContextFor(Msg(userId, "install-A", "new-token")));

        var row = await DataContext.UserDevices.SingleAsync();
        row.UserId.Should().Be(userId);
        row.FcmToken.Should().Be("new-token");
        row.NotificationsEnabled.Should().BeFalse("a same-owner token refresh must not re-enable a device the user turned off");
        row.LastSeenUtc.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Consume_OwnerChange_ReassignsUser_AndResetsOptOut()
    {
        var originalOwner = Guid.NewGuid();
        var newOwner = Guid.NewGuid();
        await SeedDeviceAsync(originalOwner, "install-A", "old-token", notificationsEnabled: false);

        var sut = Mocker.CreateInstance<UserDeviceRegisteredConsumer>();
        await sut.Consume(ContextFor(Msg(newOwner, "install-A", "new-token")));

        // Still exactly one row for the install — reassigned, not duplicated.
        var row = await DataContext.UserDevices.SingleAsync();
        row.UserId.Should().Be(newOwner);
        row.FcmToken.Should().Be("new-token");
        row.NotificationsEnabled.Should().BeTrue("a new owner starts with notifications enabled");
    }
}
