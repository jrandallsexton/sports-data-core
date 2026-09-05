using System.Linq.Expressions;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Notification.Application.Reminders.Commands.SendPickDeadlineReminder;
using SportsData.Notification.Application.Scheduling;
using SportsData.Notification.Config;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Scheduling;

// v2 wave model: the scheduler clusters a league-week's kickoffs into waves
// (coalesce window off each wave's earliest kickoff) and maintains one
// PendingScheduledJob + Hangfire delayed job per (member, wave). Wave
// derivation is exercised through the public evaluate method by asserting
// the rows it persists.
public class PickDeadlineReminderSchedulerTests : NotificationTestBase<PickDeadlineReminderScheduler>
{
    private static readonly DateTime FixedNow = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IProvideBackgroundJobs> _jobs;
    private int _jobCounter;

    public PickDeadlineReminderSchedulerTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);
        Mocker.Use<IOptions<NotificationConfig>>(Options.Create(new NotificationConfig()));

        _jobs = Mocker.GetMock<IProvideBackgroundJobs>();
        _jobs.Setup(x => x.Schedule(
                It.IsAny<Expression<Func<ISendPickDeadlineReminderCommandHandler, Task>>>(), It.IsAny<TimeSpan>()))
            .Returns(() => $"job-{++_jobCounter}");
    }

    private async Task SeedMemberAsync(Guid leagueId, Guid userId)
    {
        DataContext.PickemGroupMembers.Add(new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId,
            Role = "Member",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    private async Task SeedMatchupAsync(Guid leagueId, DateTime startDateUtc, int seasonWeek = 2)
    {
        DataContext.PickemGroupMatchups.Add(new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            ContestId = Guid.NewGuid(),
            StartDateUtc = startDateUtc,
            SeasonYear = 2026,
            SeasonWeek = seasonWeek,
            StatusTypeName = "STATUS_SCHEDULED",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Evaluate_ClusteredKickoffs_CoalesceIntoWaves()
    {
        // 17:00 / 17:15 / 17:30 chain into one wave (each within 30 of the
        // 17:00 anchor... 17:30 is exactly at the window edge); 19:00 starts
        // its own. Expect two rows: anchors 17:00 and 19:00, fires at -60.
        var leagueId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedMemberAsync(leagueId, userId);
        await SeedMatchupAsync(leagueId, FixedNow.Date.AddHours(17));
        await SeedMatchupAsync(leagueId, FixedNow.Date.AddHours(17).AddMinutes(15));
        await SeedMatchupAsync(leagueId, FixedNow.Date.AddHours(17).AddMinutes(30));
        await SeedMatchupAsync(leagueId, FixedNow.Date.AddHours(19));

        var sut = Mocker.CreateInstance<PickDeadlineReminderScheduler>();
        await sut.EvaluateAndScheduleForLeagueWeekAsync(leagueId, 2, CancellationToken.None);

        var rows = await DataContext.PendingScheduledJobs
            .AsNoTracking()
            .Where(j => j.JobKind == "PickDeadline" && j.TargetId == leagueId)
            .OrderBy(j => j.WaveAnchorUtc)
            .Select(j => new { j.UserId, j.WaveAnchorUtc, j.ScheduledFireUtc })
            .ToListAsync();

        rows.Should().HaveCount(2);
        rows[0].UserId.Should().Be(userId);
        rows[0].WaveAnchorUtc.Should().Be(FixedNow.Date.AddHours(17));
        rows[0].ScheduledFireUtc.Should().Be(FixedNow.Date.AddHours(16));
        rows[1].WaveAnchorUtc.Should().Be(FixedNow.Date.AddHours(19));
        rows[1].ScheduledFireUtc.Should().Be(FixedNow.Date.AddHours(18));
    }

    [Fact]
    public async Task Evaluate_MultipleMembers_OneRowPerMemberPerWave()
    {
        var leagueId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await SeedMemberAsync(leagueId, userA);
        await SeedMemberAsync(leagueId, userB);
        await SeedMatchupAsync(leagueId, FixedNow.AddHours(5));
        await SeedMatchupAsync(leagueId, FixedNow.AddHours(9));

        var sut = Mocker.CreateInstance<PickDeadlineReminderScheduler>();
        await sut.EvaluateAndScheduleForLeagueWeekAsync(leagueId, 2, CancellationToken.None);

        var rows = await DataContext.PendingScheduledJobs.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(4);
        rows.Select(r => r.UserId).Distinct().Should().BeEquivalentTo(new[] { userA, userB });
    }

    [Fact]
    public async Task Evaluate_WaveWithinLeadWindow_NotScheduled()
    {
        // Kickoff 30 minutes out — fire time would be in the past. Skip.
        var leagueId = Guid.NewGuid();
        await SeedMemberAsync(leagueId, Guid.NewGuid());
        await SeedMatchupAsync(leagueId, FixedNow.AddMinutes(30));

        var sut = Mocker.CreateInstance<PickDeadlineReminderScheduler>();
        await sut.EvaluateAndScheduleForLeagueWeekAsync(leagueId, 2, CancellationToken.None);

        (await DataContext.PendingScheduledJobs.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Evaluate_KickoffMoved_ReschedulesWaveAndDeletesOldJob()
    {
        var leagueId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedMemberAsync(leagueId, userId);
        await SeedMatchupAsync(leagueId, FixedNow.AddHours(6));

        var sut = Mocker.CreateInstance<PickDeadlineReminderScheduler>();
        await sut.EvaluateAndScheduleForLeagueWeekAsync(leagueId, 2, CancellationToken.None);

        var original = await DataContext.PendingScheduledJobs.AsNoTracking().SingleAsync();

        // The game slides an hour later; the old anchor's row is orphaned and
        // a fresh row lands on the new anchor.
        var matchup = await DataContext.PickemGroupMatchups.SingleAsync();
        matchup.StartDateUtc = FixedNow.AddHours(7);
        await DataContext.SaveChangesAsync();

        await sut.EvaluateAndScheduleForLeagueWeekAsync(leagueId, 2, CancellationToken.None);

        var rows = await DataContext.PendingScheduledJobs.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].WaveAnchorUtc.Should().Be(FixedNow.AddHours(7));
        rows[0].ScheduledFireUtc.Should().Be(FixedNow.AddHours(6));
        _jobs.Verify(x => x.Delete(original.HangfireJobId), Times.Once);
    }

    [Fact]
    public async Task Evaluate_AnchorDelayedWithinCoalesce_OldRowOrphanedNoDoublePush()
    {
        // The 18:00 anchor game slips to 18:10; the wave re-anchors and a new
        // schedulable row (fire 17:10) covers BOTH games. The stale 18:00 row
        // must be orphaned — keeping it fires a near-duplicate push at 17:00.
        var leagueId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedMemberAsync(leagueId, userId);
        var day = FixedNow.Date;
        await SeedMatchupAsync(leagueId, day.AddHours(18));
        await SeedMatchupAsync(leagueId, day.AddHours(18).AddMinutes(25));

        var sut = Mocker.CreateInstance<PickDeadlineReminderScheduler>();
        await sut.EvaluateAndScheduleForLeagueWeekAsync(leagueId, 2, CancellationToken.None);

        var original = await DataContext.PendingScheduledJobs.AsNoTracking().SingleAsync();
        original.WaveAnchorUtc.Should().Be(day.AddHours(18));

        var anchorGame = await DataContext.PickemGroupMatchups
            .SingleAsync(m => m.StartDateUtc == day.AddHours(18));
        anchorGame.StartDateUtc = day.AddHours(18).AddMinutes(10);
        await DataContext.SaveChangesAsync();

        // Re-evaluate at 16:55 — the re-anchored wave (18:10, fire 17:10) is
        // schedulable and covers every kickoff.
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow())
            .Returns(day.AddHours(16).AddMinutes(55));
        await sut.EvaluateAndScheduleForLeagueWeekAsync(leagueId, 2, CancellationToken.None);

        var rows = await DataContext.PendingScheduledJobs.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].WaveAnchorUtc.Should().Be(day.AddHours(18).AddMinutes(10));
        rows[0].ScheduledFireUtc.Should().Be(day.AddHours(17).AddMinutes(10));
        _jobs.Verify(x => x.Delete(original.HangfireJobId), Times.Once);
    }

    [Fact]
    public async Task Evaluate_EarlierKickoffMergesWaves_KeepsCoveringRow()
    {
        // A 18:25 game moves up to 17:40, merging the 18:00 wave into a new
        // 17:40-anchored wave whose fire time (16:40) is already past. The
        // 18:00-anchor row must SURVIVE: its window still contains the
        // unmoved 18:00 game, and it is the only fire that can cover it.
        // Anchor-set-based orphaning deleted it (silent missed reminder).
        var leagueId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedMemberAsync(leagueId, userId);
        var day = FixedNow.Date;
        await SeedMatchupAsync(leagueId, day.AddHours(18));
        await SeedMatchupAsync(leagueId, day.AddHours(18).AddMinutes(25));

        var sut = Mocker.CreateInstance<PickDeadlineReminderScheduler>();
        await sut.EvaluateAndScheduleForLeagueWeekAsync(leagueId, 2, CancellationToken.None);

        var original = await DataContext.PendingScheduledJobs.AsNoTracking().SingleAsync();
        original.WaveAnchorUtc.Should().Be(day.AddHours(18));

        var moved = await DataContext.PickemGroupMatchups
            .SingleAsync(m => m.StartDateUtc == day.AddHours(18).AddMinutes(25));
        moved.StartDateUtc = day.AddHours(17).AddMinutes(40);
        await DataContext.SaveChangesAsync();

        // Re-evaluate at 16:50 — the merged wave's fire (16:40) is past.
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow())
            .Returns(day.AddHours(16).AddMinutes(50));
        await sut.EvaluateAndScheduleForLeagueWeekAsync(leagueId, 2, CancellationToken.None);

        var rows = await DataContext.PendingScheduledJobs.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].WaveAnchorUtc.Should().Be(day.AddHours(18));
        rows[0].ScheduledFireUtc.Should().Be(day.AddHours(17));
        _jobs.Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Evaluate_V1NullAnchorRow_CleanedUp()
    {
        // Rows from the pre-wave model (null anchor) are orphans whenever
        // still future-scheduled — delete row + best-effort Hangfire job.
        var leagueId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedMemberAsync(leagueId, userId);
        await SeedMatchupAsync(leagueId, FixedNow.AddHours(6));
        DataContext.PendingScheduledJobs.Add(new PendingScheduledJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobKind = "PickDeadline",
            TargetId = leagueId,
            SeasonWeek = 2,
            WaveAnchorUtc = null,
            HangfireJobId = "v1-job",
            ScheduledFireUtc = FixedNow.AddHours(5),
            CreatedUtc = FixedNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<PickDeadlineReminderScheduler>();
        await sut.EvaluateAndScheduleForLeagueWeekAsync(leagueId, 2, CancellationToken.None);

        var rows = await DataContext.PendingScheduledJobs.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].WaveAnchorUtc.Should().Be(FixedNow.AddHours(6));
        _jobs.Verify(x => x.Delete("v1-job"), Times.Once);
    }

    [Fact]
    public async Task Evaluate_InFlightRow_NotDeleted()
    {
        // A row whose fire time has passed may be mid-dispatch — orphan
        // cleanup must leave it for the dispatcher's own gates.
        var leagueId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedMemberAsync(leagueId, userId);
        await SeedMatchupAsync(leagueId, FixedNow.AddHours(6));
        DataContext.PendingScheduledJobs.Add(new PendingScheduledJob
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobKind = "PickDeadline",
            TargetId = leagueId,
            SeasonWeek = 2,
            WaveAnchorUtc = FixedNow.AddMinutes(-30),
            HangfireJobId = "fired-job",
            ScheduledFireUtc = FixedNow.AddMinutes(-90),
            CreatedUtc = FixedNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<PickDeadlineReminderScheduler>();
        await sut.EvaluateAndScheduleForLeagueWeekAsync(leagueId, 2, CancellationToken.None);

        var rows = await DataContext.PendingScheduledJobs.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().Contain(r => r.HangfireJobId == "fired-job");
        _jobs.Verify(x => x.Delete("fired-job"), Times.Never);
    }

    [Fact]
    public async Task Evaluate_UnchangedWave_NoOps()
    {
        var leagueId = Guid.NewGuid();
        await SeedMemberAsync(leagueId, Guid.NewGuid());
        await SeedMatchupAsync(leagueId, FixedNow.AddHours(6));

        var sut = Mocker.CreateInstance<PickDeadlineReminderScheduler>();
        await sut.EvaluateAndScheduleForLeagueWeekAsync(leagueId, 2, CancellationToken.None);
        await sut.EvaluateAndScheduleForLeagueWeekAsync(leagueId, 2, CancellationToken.None);

        (await DataContext.PendingScheduledJobs.CountAsync()).Should().Be(1);
        _jobs.Verify(x => x.Schedule(
                It.IsAny<Expression<Func<ISendPickDeadlineReminderCommandHandler, Task>>>(), It.IsAny<TimeSpan>()),
            Times.Once);
        _jobs.Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Evaluate_OptedOutMember_NotScheduled()
    {
        var leagueId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedMemberAsync(leagueId, userId);
        await SeedMatchupAsync(leagueId, FixedNow.AddHours(6));
        DataContext.UserNotificationPreferences.Add(new UserNotificationPreferences
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickDeadlineReminderEnabled = false,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<PickDeadlineReminderScheduler>();
        await sut.EvaluateAndScheduleForLeagueWeekAsync(leagueId, 2, CancellationToken.None);

        (await DataContext.PendingScheduledJobs.AnyAsync()).Should().BeFalse();
    }
}
