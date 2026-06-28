using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Eventing.Events.Users;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Consumers;

public class UserDeviceUnregisteredConsumerTests : NotificationTestBase<UserDeviceUnregisteredConsumer>
{
    private static readonly DateTime FixedNow = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    private static ConsumeContext<UserDeviceUnregistered> ContextFor(UserDeviceUnregistered msg)
    {
        var ctx = new Mock<ConsumeContext<UserDeviceUnregistered>>();
        ctx.SetupGet(x => x.Message).Returns(msg);
        ctx.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private async Task SeedDeviceAsync(Guid userId, string installationId)
    {
        DataContext.UserDevices.Add(new UserDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            InstallationId = installationId,
            FcmToken = "tok",
            Platform = "ios",
            NotificationsEnabled = true,
            LastSeenUtc = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Consume_DeletesOwnRow()
    {
        var userId = Guid.NewGuid();
        await SeedDeviceAsync(userId, "install-A");

        var sut = Mocker.CreateInstance<UserDeviceUnregisteredConsumer>();
        await sut.Consume(ContextFor(new UserDeviceUnregistered(userId, "install-A", Guid.NewGuid(), Guid.NewGuid())));

        (await DataContext.UserDevices.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Consume_DoesNotDeleteRowOwnedByDifferentUser()
    {
        var owner = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        await SeedDeviceAsync(owner, "install-A");

        var sut = Mocker.CreateInstance<UserDeviceUnregisteredConsumer>();
        await sut.Consume(ContextFor(new UserDeviceUnregistered(someoneElse, "install-A", Guid.NewGuid(), Guid.NewGuid())));

        var row = await DataContext.UserDevices.SingleAsync();
        row.UserId.Should().Be(owner, "a user must not be able to unregister a device another account now owns");
    }

    [Fact]
    public async Task Consume_IsNoOp_WhenRowAbsent()
    {
        var sut = Mocker.CreateInstance<UserDeviceUnregisteredConsumer>();

        var act = async () => await sut.Consume(
            ContextFor(new UserDeviceUnregistered(Guid.NewGuid(), "missing", Guid.NewGuid(), Guid.NewGuid())));

        await act.Should().NotThrowAsync();
        (await DataContext.UserDevices.AnyAsync()).Should().BeFalse();
    }
}
