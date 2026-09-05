using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Application.Scheduling;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Consumers;

public class PickemGroupMatchupDataPublishedConsumerTests
    : NotificationTestBase<PickemGroupMatchupDataPublishedConsumer>
{
    private static readonly DateTime FixedNow = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IPickDeadlineReminderScheduler> _reminderScheduler;
    private readonly Mock<IContestStartReminderScheduler> _contestStartScheduler;

    public PickemGroupMatchupDataPublishedConsumerTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);

        _reminderScheduler = Mocker.GetMock<IPickDeadlineReminderScheduler>();
        _contestStartScheduler = Mocker.GetMock<IContestStartReminderScheduler>();
    }

    private static ConsumeContext<PickemGroupMatchupDataPublished> ContextFor(
        PickemGroupMatchupDataPublished msg)
    {
        var ctx = new Mock<ConsumeContext<PickemGroupMatchupDataPublished>>();
        ctx.SetupGet(x => x.Message).Returns(msg);
        ctx.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private static PickemGroupMatchupDataPublished Msg(
        Guid groupId, Guid contestId, DateTime startDateUtc, int seasonWeek = 2, int seasonYear = 2026)
        => new(groupId, contestId, startDateUtc, seasonWeek, Sport.FootballNcaa,
            seasonYear, Guid.NewGuid(), Guid.NewGuid());

    private async Task SeedProjectionAsync(
        Guid groupId, Guid contestId, DateTime startDateUtc, int seasonWeek = 2, int seasonYear = 2026)
    {
        DataContext.PickemGroupMatchups.Add(new PickemGroupMatchup
        {
            PickemGroupId = groupId,
            ContestId = contestId,
            StartDateUtc = startDateUtc,
            StartDateUpdatedAt = FixedNow.AddDays(-10),
            SeasonYear = seasonYear,
            SeasonWeek = seasonWeek,
            StatusTypeName = "STATUS_SCHEDULED",
            CreatedUtc = FixedNow.AddDays(-10),
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Consume_UnchangedProjection_StillEvaluatesSchedulers()
    {
        // The backfill exists to heal missing SCHEDULES, not projections.
        // A projection can be fully current while its reminders were never
        // scheduled (pre-scheduler image consumed the original events).
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var start = FixedNow.AddDays(1);
        await SeedProjectionAsync(groupId, contestId, start);

        var sut = Mocker.CreateInstance<PickemGroupMatchupDataPublishedConsumer>();
        await sut.Consume(ContextFor(Msg(groupId, contestId, start)));

        _reminderScheduler.Verify(
            x => x.EvaluateAndScheduleForLeagueWeekAsync(groupId, 2, It.IsAny<CancellationToken>()),
            Times.Once);
        _contestStartScheduler.Verify(
            x => x.EvaluateAndScheduleForContestAsync(contestId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_UnchangedProjection_DoesNotStampModified()
    {
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var start = FixedNow.AddDays(1);
        await SeedProjectionAsync(groupId, contestId, start);

        var sut = Mocker.CreateInstance<PickemGroupMatchupDataPublishedConsumer>();
        await sut.Consume(ContextFor(Msg(groupId, contestId, start)));

        var row = await DataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.PickemGroupId == groupId && m.ContestId == contestId)
            .Select(m => new { m.ModifiedUtc })
            .SingleAsync();
        row.ModifiedUtc.Should().BeNull();
    }

    [Fact]
    public async Task Consume_NewProjection_InsertsAndSchedules()
    {
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var start = FixedNow.AddDays(1);

        var sut = Mocker.CreateInstance<PickemGroupMatchupDataPublishedConsumer>();
        await sut.Consume(ContextFor(Msg(groupId, contestId, start)));

        var row = await DataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.PickemGroupId == groupId && m.ContestId == contestId)
            .Select(m => new { m.StartDateUtc, m.StatusTypeName })
            .SingleAsync();
        row.StartDateUtc.Should().Be(start);
        row.StatusTypeName.Should().Be("STATUS_SCHEDULED");

        _reminderScheduler.Verify(
            x => x.EvaluateAndScheduleForLeagueWeekAsync(groupId, 2, It.IsAny<CancellationToken>()),
            Times.Once);
        _contestStartScheduler.Verify(
            x => x.EvaluateAndScheduleForContestAsync(contestId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_WeekChanged_ReevaluatesPriorAndCurrentWeek()
    {
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var start = FixedNow.AddDays(1);
        await SeedProjectionAsync(groupId, contestId, start, seasonWeek: 1);

        var sut = Mocker.CreateInstance<PickemGroupMatchupDataPublishedConsumer>();
        await sut.Consume(ContextFor(Msg(groupId, contestId, start, seasonWeek: 2)));

        _reminderScheduler.Verify(
            x => x.EvaluateAndScheduleForLeagueWeekAsync(groupId, 1, It.IsAny<CancellationToken>()),
            Times.Once);
        _reminderScheduler.Verify(
            x => x.EvaluateAndScheduleForLeagueWeekAsync(groupId, 2, It.IsAny<CancellationToken>()),
            Times.Once);
        _contestStartScheduler.Verify(
            x => x.EvaluateAndScheduleForContestAsync(contestId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_SeasonYearOnlyChanged_UpdatesProjectionAndSchedules()
    {
        // Regression: scheduling was gated on startDateChanged || weekChanged,
        // so a SeasonYear-only update skipped both schedulers.
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var start = FixedNow.AddDays(1);
        await SeedProjectionAsync(groupId, contestId, start, seasonYear: 2025);

        var sut = Mocker.CreateInstance<PickemGroupMatchupDataPublishedConsumer>();
        await sut.Consume(ContextFor(Msg(groupId, contestId, start, seasonYear: 2026)));

        var row = await DataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.PickemGroupId == groupId && m.ContestId == contestId)
            .Select(m => new { m.SeasonYear, m.StartDateUtc, m.SeasonWeek })
            .SingleAsync();
        row.SeasonYear.Should().Be(2026);
        row.StartDateUtc.Should().Be(start);
        row.SeasonWeek.Should().Be(2);

        _reminderScheduler.Verify(
            x => x.EvaluateAndScheduleForLeagueWeekAsync(groupId, 2, It.IsAny<CancellationToken>()),
            Times.Once);
        _contestStartScheduler.Verify(
            x => x.EvaluateAndScheduleForContestAsync(contestId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_StartDateChanged_UpdatesProjectionAndSchedules()
    {
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedProjectionAsync(groupId, contestId, FixedNow.AddDays(1));

        var newStart = FixedNow.AddDays(2);
        var sut = Mocker.CreateInstance<PickemGroupMatchupDataPublishedConsumer>();
        await sut.Consume(ContextFor(Msg(groupId, contestId, newStart)));

        var row = await DataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.PickemGroupId == groupId && m.ContestId == contestId)
            .Select(m => new { m.StartDateUtc, m.ModifiedUtc })
            .SingleAsync();
        row.StartDateUtc.Should().Be(newStart);
        row.ModifiedUtc.Should().Be(FixedNow);

        _reminderScheduler.Verify(
            x => x.EvaluateAndScheduleForLeagueWeekAsync(groupId, 2, It.IsAny<CancellationToken>()),
            Times.Once);
        _contestStartScheduler.Verify(
            x => x.EvaluateAndScheduleForContestAsync(contestId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
