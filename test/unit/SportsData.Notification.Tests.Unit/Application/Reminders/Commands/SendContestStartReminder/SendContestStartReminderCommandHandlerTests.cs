using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Notification.Application.Reminders;
using SportsData.Notification.Application.Reminders.Commands.SendContestStartReminder;
using SportsData.Notification.Infrastructure.Data.Entities;
using SportsData.Notification.Infrastructure.Notifications;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Reminders.Commands.SendContestStartReminder;

// Contest-start dispatch: atomic claim into NotificationContestStart, stale-
// fire via PendingScheduledJobs (null SeasonWeek + null WaveAnchorUtc), and
// sport-aware copy. StaleFireGuard and PushDeviceFanout run real against the
// InMemory context so these stay end-to-end over the slice.
public class SendContestStartReminderCommandHandlerTests
    : NotificationTestBase<SendContestStartReminderCommandHandler>
{
    private static readonly DateTime FixedNow = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FireTime = new(2026, 9, 5, 16, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IPushNotificationSender> _pushSender;

    public SendContestStartReminderCommandHandlerTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);

        _pushSender = Mocker.GetMock<IPushNotificationSender>();
        _pushSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<string>("msg-id"));

        Mocker.Use<IStaleFireGuard>(Mocker.CreateInstance<StaleFireGuard>());
        Mocker.Use<IPushDeviceFanout>(Mocker.CreateInstance<PushDeviceFanout>());
    }

    private sealed record ClaimView(
        Guid UserId, Guid ContestId, DateTime FireTimeUtc, Guid CorrelationId, string Result);

    private Task<ClaimView> GetSingleClaimAsync() =>
        DataContext.NotificationContestStarts
            .AsNoTracking()
            .Select(c => new ClaimView(c.UserId, c.ContestId, c.FireTimeUtc, c.CorrelationId, c.Result))
            .SingleAsync();

    private void VerifySendCount(Times times) =>
        _pushSender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), times);

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

    private async Task SeedScheduleAsync(Guid userId, Guid contestId, DateTime fireTimeUtc)
    {
        DataContext.PendingScheduledJobs.Add(new PendingScheduledJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobKind = "ContestStart",
            TargetId = contestId,
            SeasonWeek = null,
            WaveAnchorUtc = null,
            HangfireJobId = "job-1",
            ScheduledFireUtc = fireTimeUtc,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Execute_MatchingScheduleAndDevice_NotifiesAndPersistsRow()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedDeviceAsync(userId);
        await SeedScheduleAsync(userId, contestId, FireTime);

        var sut = Mocker.CreateInstance<SendContestStartReminderCommandHandler>();
        await sut.ExecuteAsync(userId, contestId, FireTime);

        VerifySendCount(Times.Once());
        var row = await GetSingleClaimAsync();
        row.UserId.Should().Be(userId);
        row.ContestId.Should().Be(contestId);
        row.FireTimeUtc.Should().Be(FireTime);
        row.CorrelationId.Should().NotBeEmpty();
        row.Result.Should().Be("Sent");
    }

    [Fact]
    public async Task Execute_OptedOut_SuppressedAndDoesNotNotify()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedDeviceAsync(userId);
        await SeedScheduleAsync(userId, contestId, FireTime);
        DataContext.UserNotificationPreferences.Add(new UserNotificationPreferences
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ContestStartReminderEnabled = false,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<SendContestStartReminderCommandHandler>();
        await sut.ExecuteAsync(userId, contestId, FireTime);

        VerifySendCount(Times.Never());
        var row = await GetSingleClaimAsync();
        row.Result.Should().Be("Suppressed_UserOptedOut");
    }

    [Fact]
    public async Task Execute_NoScheduleRow_SuppressedStaleFire()
    {
        // No PendingScheduledJob → the fire is an orphan; suppress before sending.
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<SendContestStartReminderCommandHandler>();
        await sut.ExecuteAsync(userId, contestId, FireTime);

        VerifySendCount(Times.Never());
        var row = await GetSingleClaimAsync();
        row.Result.Should().Be("Suppressed_StaleFire");
    }

    [Fact]
    public async Task Execute_NoDevices_SuppressedAfterGates()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedScheduleAsync(userId, contestId, FireTime);

        var sut = Mocker.CreateInstance<SendContestStartReminderCommandHandler>();
        await sut.ExecuteAsync(userId, contestId, FireTime);

        VerifySendCount(Times.Never());
        var row = await GetSingleClaimAsync();
        row.Result.Should().Be("Suppressed_NoDevice");
    }
}
