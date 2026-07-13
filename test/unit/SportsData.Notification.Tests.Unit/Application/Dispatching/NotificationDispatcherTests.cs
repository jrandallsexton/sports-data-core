using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Infrastructure.Data.Entities;
using SportsData.Notification.Infrastructure.Notifications;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Dispatching;

// Covers the two scheduled-reminder paths claiming/dispatching into the typed
// NotificationPickDeadline / NotificationContestStart tables. The stale-fire
// check reads PendingScheduledJobs, so the happy paths seed a matching row.
public class NotificationDispatcherTests : NotificationTestBase<NotificationDispatcher>
{
    private static readonly DateTime FixedNow = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FireTime = new(2026, 9, 5, 16, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IPushNotificationSender> _pushSender;

    public NotificationDispatcherTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);

        _pushSender = Mocker.GetMock<IPushNotificationSender>();
        _pushSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<string>("msg-id"));
    }

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

    private async Task SeedScheduleAsync(Guid userId, string jobKind, Guid targetId, int? seasonWeek, DateTime fireTimeUtc)
    {
        DataContext.PendingScheduledJobs.Add(new PendingScheduledJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobKind = jobKind,
            TargetId = targetId,
            SeasonWeek = seasonWeek,
            HangfireJobId = "job-1",
            ScheduledFireUtc = fireTimeUtc,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    [Fact]
    public async Task PickDeadline_MatchingScheduleAndDevice_NotifiesAndPersistsRow()
    {
        var userId = Guid.NewGuid();
        var leagueId = Guid.NewGuid();
        await SeedDeviceAsync(userId);
        await SeedScheduleAsync(userId, "PickDeadline", leagueId, seasonWeek: 3, FireTime);

        var sut = Mocker.CreateInstance<NotificationDispatcher>();
        await sut.SendPickDeadlineReminderAsync(userId, leagueId, 3, FireTime);

        VerifySendCount(Times.Once());
        var row = await DataContext.NotificationPickDeadlines.SingleAsync();
        row.UserId.Should().Be(userId);
        row.LeagueId.Should().Be(leagueId);
        row.SeasonWeek.Should().Be(3);
        row.FireTimeUtc.Should().Be(FireTime);
        row.CorrelationId.Should().NotBeEmpty();
        row.Result.Should().Be("Sent");
    }

    [Fact]
    public async Task PickDeadline_NoScheduleRow_SuppressedStaleFire()
    {
        // No PendingScheduledJob → the fire is an orphan; suppress before sending.
        var userId = Guid.NewGuid();
        var leagueId = Guid.NewGuid();
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<NotificationDispatcher>();
        await sut.SendPickDeadlineReminderAsync(userId, leagueId, 3, FireTime);

        VerifySendCount(Times.Never());
        var row = await DataContext.NotificationPickDeadlines.SingleAsync();
        row.Result.Should().Be("Suppressed_StaleFire");
    }

    [Fact]
    public async Task PickDeadline_OptedOut_SuppressedAndDoesNotNotify()
    {
        var userId = Guid.NewGuid();
        var leagueId = Guid.NewGuid();
        await SeedDeviceAsync(userId);
        await SeedScheduleAsync(userId, "PickDeadline", leagueId, seasonWeek: 3, FireTime);
        DataContext.UserNotificationPreferences.Add(new UserNotificationPreferences
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickDeadlineReminderEnabled = false,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<NotificationDispatcher>();
        await sut.SendPickDeadlineReminderAsync(userId, leagueId, 3, FireTime);

        VerifySendCount(Times.Never());
        var row = await DataContext.NotificationPickDeadlines.SingleAsync();
        row.Result.Should().Be("Suppressed_UserOptedOut");
    }

    [Fact]
    public async Task ContestStart_MatchingScheduleAndDevice_NotifiesAndPersistsRow()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedDeviceAsync(userId);
        await SeedScheduleAsync(userId, "ContestStart", contestId, seasonWeek: null, FireTime);

        var sut = Mocker.CreateInstance<NotificationDispatcher>();
        await sut.SendContestStartReminderAsync(userId, contestId, FireTime);

        VerifySendCount(Times.Once());
        var row = await DataContext.NotificationContestStarts.SingleAsync();
        row.UserId.Should().Be(userId);
        row.ContestId.Should().Be(contestId);
        row.FireTimeUtc.Should().Be(FireTime);
        row.CorrelationId.Should().NotBeEmpty();
        row.Result.Should().Be("Sent");
    }

    [Fact]
    public async Task ContestStart_OptedOut_SuppressedAndDoesNotNotify()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedDeviceAsync(userId);
        await SeedScheduleAsync(userId, "ContestStart", contestId, seasonWeek: null, FireTime);
        DataContext.UserNotificationPreferences.Add(new UserNotificationPreferences
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ContestStartReminderEnabled = false,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<NotificationDispatcher>();
        await sut.SendContestStartReminderAsync(userId, contestId, FireTime);

        VerifySendCount(Times.Never());
        var row = await DataContext.NotificationContestStarts.SingleAsync();
        row.Result.Should().Be("Suppressed_UserOptedOut");
    }

    [Fact]
    public async Task ContestStart_NoScheduleRow_SuppressedStaleFire()
    {
        // No PendingScheduledJob → the fire is an orphan; suppress before sending.
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<NotificationDispatcher>();
        await sut.SendContestStartReminderAsync(userId, contestId, FireTime);

        VerifySendCount(Times.Never());
        var row = await DataContext.NotificationContestStarts.SingleAsync();
        row.Result.Should().Be("Suppressed_StaleFire");
    }
}
