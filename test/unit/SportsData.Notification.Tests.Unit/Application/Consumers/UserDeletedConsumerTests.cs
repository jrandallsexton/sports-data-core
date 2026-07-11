using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Eventing.Events.Users;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Consumers;

public class UserDeletedConsumerTests : NotificationTestBase<UserDeletedConsumer>
{
    private static readonly DateTime FixedNow = new(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

    private static ConsumeContext<UserDeleted> ContextFor(UserDeleted msg)
    {
        var ctx = new Mock<ConsumeContext<UserDeleted>>();
        ctx.SetupGet(x => x.Message).Returns(msg);
        ctx.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private async Task SeedFootprintAsync(Guid userId)
    {
        DataContext.Users.Add(new User { Id = userId, DisplayName = "X", Email = "x@x.com", CreatedBy = Guid.NewGuid() });
        DataContext.UserDevices.Add(new UserDevice
        {
            Id = Guid.NewGuid(), UserId = userId, InstallationId = Guid.NewGuid().ToString(),
            FcmToken = "t", Platform = "ios", NotificationsEnabled = true, CreatedBy = Guid.NewGuid()
        });
        DataContext.UserNotificationPreferences.Add(new UserNotificationPreferences { Id = Guid.NewGuid(), UserId = userId, CreatedBy = Guid.NewGuid() });
        DataContext.UserPicks.Add(new UserPick { Id = Guid.NewGuid(), UserId = userId, ContestId = Guid.NewGuid(), PickemGroupId = Guid.NewGuid(), CreatedBy = Guid.NewGuid() });
        DataContext.PendingScheduledJobs.Add(new PendingScheduledJob { Id = Guid.NewGuid(), UserId = userId, JobKind = "k", TargetId = Guid.NewGuid(), HangfireJobId = "hf", ScheduledFireUtc = FixedNow, CreatedBy = Guid.NewGuid() });
        DataContext.NotificationLog.Add(new NotificationLog { Id = Guid.NewGuid(), UserId = userId, CorrelationId = Guid.NewGuid(), Category = "c", Channel = "Fcm", Result = "Sent", AttemptedUtc = FixedNow, CreatedBy = Guid.NewGuid() });
        DataContext.PickemGroupMembers.Add(new PickemGroupMember { Id = Guid.NewGuid(), UserId = userId, PickemGroupId = Guid.NewGuid(), Role = "Member", CreatedBy = Guid.NewGuid() });
        await DataContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Consume_PurgesEverythingForThatUser_LeavesOthers()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await SeedFootprintAsync(userId);
        await SeedFootprintAsync(otherUserId);

        var sut = Mocker.CreateInstance<UserDeletedConsumer>();
        await sut.Consume(ContextFor(new UserDeleted(userId, Guid.NewGuid(), Guid.NewGuid())));

        (await DataContext.Users.AnyAsync(x => x.Id == userId)).Should().BeFalse();
        (await DataContext.UserDevices.AnyAsync(x => x.UserId == userId)).Should().BeFalse();
        (await DataContext.UserNotificationPreferences.AnyAsync(x => x.UserId == userId)).Should().BeFalse();
        (await DataContext.UserPicks.AnyAsync(x => x.UserId == userId)).Should().BeFalse();
        (await DataContext.PendingScheduledJobs.AnyAsync(x => x.UserId == userId)).Should().BeFalse();
        (await DataContext.NotificationLog.AnyAsync(x => x.UserId == userId)).Should().BeFalse();
        (await DataContext.PickemGroupMembers.AnyAsync(x => x.UserId == userId)).Should().BeFalse();

        // Other user's footprint is untouched across every purged table.
        (await DataContext.Users.AnyAsync(x => x.Id == otherUserId)).Should().BeTrue();
        (await DataContext.UserDevices.AnyAsync(x => x.UserId == otherUserId)).Should().BeTrue();
        (await DataContext.UserNotificationPreferences.AnyAsync(x => x.UserId == otherUserId)).Should().BeTrue();
        (await DataContext.UserPicks.AnyAsync(x => x.UserId == otherUserId)).Should().BeTrue();
        (await DataContext.PendingScheduledJobs.AnyAsync(x => x.UserId == otherUserId)).Should().BeTrue();
        (await DataContext.NotificationLog.AnyAsync(x => x.UserId == otherUserId)).Should().BeTrue();
        (await DataContext.PickemGroupMembers.AnyAsync(x => x.UserId == otherUserId)).Should().BeTrue();
    }

    [Fact]
    public async Task Consume_SameMessageTwice_IsIdempotent()
    {
        var userId = Guid.NewGuid();
        await SeedFootprintAsync(userId);
        var sut = Mocker.CreateInstance<UserDeletedConsumer>();
        var msg = new UserDeleted(userId, Guid.NewGuid(), Guid.NewGuid());

        await sut.Consume(ContextFor(msg)); // first delivery purges everything

        // At-least-once redelivery: the second consume finds no rows and must be
        // a harmless no-op rather than throwing.
        var act = async () => await sut.Consume(ContextFor(msg));
        await act.Should().NotThrowAsync();

        // Every purged table stays empty for the user after the redelivery.
        (await DataContext.Users.AnyAsync(x => x.Id == userId)).Should().BeFalse();
        (await DataContext.UserDevices.AnyAsync(x => x.UserId == userId)).Should().BeFalse();
        (await DataContext.UserNotificationPreferences.AnyAsync(x => x.UserId == userId)).Should().BeFalse();
        (await DataContext.UserPicks.AnyAsync(x => x.UserId == userId)).Should().BeFalse();
        (await DataContext.PendingScheduledJobs.AnyAsync(x => x.UserId == userId)).Should().BeFalse();
        (await DataContext.NotificationLog.AnyAsync(x => x.UserId == userId)).Should().BeFalse();
        (await DataContext.PickemGroupMembers.AnyAsync(x => x.UserId == userId)).Should().BeFalse();
    }
}
