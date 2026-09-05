using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Moq;

using SportsData.Core.Common;
using SportsData.Notification.Application.Reminders;
using SportsData.Notification.Application.Reminders.Commands.SendPickDeadlineReminder;
using SportsData.Notification.Config;
using SportsData.Notification.Infrastructure.Data.Entities;
using SportsData.Notification.Infrastructure.Notifications;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Reminders.Commands.SendPickDeadlineReminder;

// v2 wave-model pick-deadline dispatch: fires carry a wave anchor, and the
// missing-pick gate decides at fire time whether anything is sent. The
// stale-fire check reads PendingScheduledJobs, so happy paths seed a
// matching row. StaleFireGuard and PushDeviceFanout run real against the
// InMemory context so these stay end-to-end over the slice.
public class SendPickDeadlineReminderCommandHandlerTests
    : NotificationTestBase<SendPickDeadlineReminderCommandHandler>
{
    private static readonly DateTime FixedNow = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FireTime = new(2026, 9, 5, 16, 0, 0, DateTimeKind.Utc);

    // Default config: lead 60 → wave anchor is one hour after the fire.
    private static readonly DateTime WaveAnchor = new(2026, 9, 5, 17, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IPushNotificationSender> _pushSender;

    public SendPickDeadlineReminderCommandHandlerTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);
        Mocker.Use<IOptions<NotificationConfig>>(Options.Create(new NotificationConfig()));

        _pushSender = Mocker.GetMock<IPushNotificationSender>();
        _pushSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<string>("msg-id"));

        Mocker.Use<IStaleFireGuard>(Mocker.CreateInstance<StaleFireGuard>());
        Mocker.Use<IPushDeviceFanout>(Mocker.CreateInstance<PushDeviceFanout>());
    }

    private sealed record ClaimView(
        Guid UserId, Guid LeagueId, int SeasonWeek, DateTime FireTimeUtc, DateTime? WaveAnchorUtc,
        Guid CorrelationId, string Result, string Body);

    private Task<ClaimView> GetSingleClaimAsync() =>
        DataContext.NotificationPickDeadlines
            .AsNoTracking()
            .Select(c => new ClaimView(
                c.UserId, c.LeagueId, c.SeasonWeek, c.FireTimeUtc, c.WaveAnchorUtc,
                c.CorrelationId, c.Result, c.Body))
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

    private async Task SeedScheduleAsync(Guid userId, Guid leagueId, DateTime fireTimeUtc, DateTime waveAnchorUtc)
    {
        DataContext.PendingScheduledJobs.Add(new PendingScheduledJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobKind = "PickDeadline",
            TargetId = leagueId,
            SeasonWeek = 3,
            WaveAnchorUtc = waveAnchorUtc,
            HangfireJobId = "job-1",
            ScheduledFireUtc = fireTimeUtc,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    private async Task<Guid> SeedWaveMatchupAsync(
        Guid leagueId, DateTime startDateUtc, int seasonWeek = 3, string headline = null)
    {
        var contestId = Guid.NewGuid();
        DataContext.PickemGroupMatchups.Add(new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            ContestId = contestId,
            StartDateUtc = startDateUtc,
            SeasonYear = 2026,
            SeasonWeek = seasonWeek,
            StatusTypeName = "STATUS_SCHEDULED",
            Headline = headline,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
        return contestId;
    }

    private async Task SeedPickAsync(Guid userId, Guid leagueId, Guid contestId)
    {
        DataContext.UserPicks.Add(new UserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ContestId = contestId,
            PickemGroupId = leagueId,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Execute_UnpickedMatchupInWave_NotifiesAndPersistsRow()
    {
        var userId = Guid.NewGuid();
        var leagueId = Guid.NewGuid();
        await SeedDeviceAsync(userId);
        await SeedScheduleAsync(userId, leagueId, FireTime, WaveAnchor);
        await SeedWaveMatchupAsync(leagueId, WaveAnchor);

        var sut = Mocker.CreateInstance<SendPickDeadlineReminderCommandHandler>();
        await sut.ExecuteAsync(userId, leagueId, 3, FireTime, WaveAnchor);

        VerifySendCount(Times.Once());
        var row = await GetSingleClaimAsync();
        row.UserId.Should().Be(userId);
        row.LeagueId.Should().Be(leagueId);
        row.SeasonWeek.Should().Be(3);
        row.FireTimeUtc.Should().Be(FireTime);
        row.WaveAnchorUtc.Should().Be(WaveAnchor);
        row.CorrelationId.Should().NotBeEmpty();
        row.Result.Should().Be("Sent");
    }

    [Fact]
    public async Task Execute_AllPicked_SuppressedAndDoesNotNotify()
    {
        // The core v2 promise: a pick on file means silence.
        var userId = Guid.NewGuid();
        var leagueId = Guid.NewGuid();
        await SeedDeviceAsync(userId);
        await SeedScheduleAsync(userId, leagueId, FireTime, WaveAnchor);
        var contestId = await SeedWaveMatchupAsync(leagueId, WaveAnchor);
        await SeedPickAsync(userId, leagueId, contestId);

        var sut = Mocker.CreateInstance<SendPickDeadlineReminderCommandHandler>();
        await sut.ExecuteAsync(userId, leagueId, 3, FireTime, WaveAnchor);

        VerifySendCount(Times.Never());
        var row = await GetSingleClaimAsync();
        row.Result.Should().Be("Suppressed_AllPicked");
    }

    [Fact]
    public async Task Execute_SingleUnpickedWithHeadline_BodyNamesTheMatchup()
    {
        var userId = Guid.NewGuid();
        var leagueId = Guid.NewGuid();
        await SeedDeviceAsync(userId);
        await SeedScheduleAsync(userId, leagueId, FireTime, WaveAnchor);
        await SeedWaveMatchupAsync(leagueId, WaveAnchor, headline: "Idaho Vandals at Utah Utes");

        // A second wave matchup, already picked — must not appear in the copy.
        var pickedContestId = await SeedWaveMatchupAsync(
            leagueId, WaveAnchor.AddMinutes(15), headline: "Other at Game");
        await SeedPickAsync(userId, leagueId, pickedContestId);

        var sut = Mocker.CreateInstance<SendPickDeadlineReminderCommandHandler>();
        await sut.ExecuteAsync(userId, leagueId, 3, FireTime, WaveAnchor);

        VerifySendCount(Times.Once());
        var row = await GetSingleClaimAsync();
        row.Result.Should().Be("Sent");
        row.Body.Should().Contain("Idaho Vandals at Utah Utes");
        row.Body.Should().NotContain("Other at Game");
    }

    [Fact]
    public async Task Execute_MultipleUnpicked_BodyCarriesCountOnly()
    {
        var userId = Guid.NewGuid();
        var leagueId = Guid.NewGuid();
        await SeedDeviceAsync(userId);
        await SeedScheduleAsync(userId, leagueId, FireTime, WaveAnchor);
        await SeedWaveMatchupAsync(leagueId, WaveAnchor, headline: "Idaho Vandals at Utah Utes");
        await SeedWaveMatchupAsync(leagueId, WaveAnchor.AddMinutes(15), headline: "Aggies at Broncos");
        await SeedWaveMatchupAsync(leagueId, WaveAnchor.AddMinutes(30), headline: "Rams at Wolf Pack");

        var sut = Mocker.CreateInstance<SendPickDeadlineReminderCommandHandler>();
        await sut.ExecuteAsync(userId, leagueId, 3, FireTime, WaveAnchor);

        VerifySendCount(Times.Once());
        var row = await GetSingleClaimAsync();
        row.Result.Should().Be("Sent");
        row.Body.Should().Contain("3 picks");
        row.Body.Should().NotContain("Idaho Vandals");
    }

    [Fact]
    public async Task Execute_MatchupOutsideCoalesceWindow_NotCounted()
    {
        // A kickoff past anchor + coalesce (default 30) belongs to the NEXT
        // wave — this fire must not claim it.
        var userId = Guid.NewGuid();
        var leagueId = Guid.NewGuid();
        await SeedDeviceAsync(userId);
        await SeedScheduleAsync(userId, leagueId, FireTime, WaveAnchor);
        await SeedWaveMatchupAsync(leagueId, WaveAnchor, headline: "Idaho Vandals at Utah Utes");
        await SeedWaveMatchupAsync(leagueId, WaveAnchor.AddMinutes(45), headline: "Later at Game");

        var sut = Mocker.CreateInstance<SendPickDeadlineReminderCommandHandler>();
        await sut.ExecuteAsync(userId, leagueId, 3, FireTime, WaveAnchor);

        var row = await GetSingleClaimAsync();
        row.Body.Should().Contain("Idaho Vandals at Utah Utes");
    }

    [Fact]
    public async Task Execute_NoMatchupsInWave_Suppressed()
    {
        // All the wave's kickoffs moved after scheduling; nothing to remind.
        var userId = Guid.NewGuid();
        var leagueId = Guid.NewGuid();
        await SeedDeviceAsync(userId);
        await SeedScheduleAsync(userId, leagueId, FireTime, WaveAnchor);

        var sut = Mocker.CreateInstance<SendPickDeadlineReminderCommandHandler>();
        await sut.ExecuteAsync(userId, leagueId, 3, FireTime, WaveAnchor);

        VerifySendCount(Times.Never());
        var row = await GetSingleClaimAsync();
        row.Result.Should().Be("Suppressed_NoMatchups");
    }

    [Fact]
    public async Task Execute_NoScheduleRow_SuppressedStaleFire()
    {
        // No PendingScheduledJob → the fire is an orphan; suppress before sending.
        var userId = Guid.NewGuid();
        var leagueId = Guid.NewGuid();
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<SendPickDeadlineReminderCommandHandler>();
        await sut.ExecuteAsync(userId, leagueId, 3, FireTime, WaveAnchor);

        VerifySendCount(Times.Never());
        var row = await GetSingleClaimAsync();
        row.Result.Should().Be("Suppressed_StaleFire");
    }

    [Fact]
    public async Task Execute_OptedOut_SuppressedAndDoesNotNotify()
    {
        var userId = Guid.NewGuid();
        var leagueId = Guid.NewGuid();
        await SeedDeviceAsync(userId);
        await SeedScheduleAsync(userId, leagueId, FireTime, WaveAnchor);
        await SeedWaveMatchupAsync(leagueId, WaveAnchor);
        DataContext.UserNotificationPreferences.Add(new UserNotificationPreferences
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickDeadlineReminderEnabled = false,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<SendPickDeadlineReminderCommandHandler>();
        await sut.ExecuteAsync(userId, leagueId, 3, FireTime, WaveAnchor);

        VerifySendCount(Times.Never());
        var row = await GetSingleClaimAsync();
        row.Result.Should().Be("Suppressed_UserOptedOut");
    }

    [Fact]
    public async Task Execute_NoDevices_SuppressedAfterGates()
    {
        var userId = Guid.NewGuid();
        var leagueId = Guid.NewGuid();
        await SeedScheduleAsync(userId, leagueId, FireTime, WaveAnchor);
        await SeedWaveMatchupAsync(leagueId, WaveAnchor);

        var sut = Mocker.CreateInstance<SendPickDeadlineReminderCommandHandler>();
        await sut.ExecuteAsync(userId, leagueId, 3, FireTime, WaveAnchor);

        VerifySendCount(Times.Never());
        var row = await GetSingleClaimAsync();
        row.Result.Should().Be("Suppressed_NoDevice");
    }
}
