using System.Linq.Expressions;

using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions;
using SportsData.Producer.Application.Consumers;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Consumers;

/// <summary>
/// Worker-side handler is genuinely a thin wrapper around
/// <see cref="CompetitionStreamScheduler.RescheduleForContestAsync"/>.
/// We exercise it with a real scheduler so the test proves the wire-up
/// (DI, parameter passing) and not just a Moq.Verify call against an
/// interface that doesn't exist.
/// </summary>
public class ContestStartTimeUpdatedConsumerHandlerTests
    : ProducerTestBase<ContestStartTimeUpdatedConsumerHandler>
{
    private static readonly DateTime FixedNow = new(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SeasonWeekId = Guid.NewGuid();

    private readonly ContestStartTimeUpdatedConsumerHandler _sut;

    public ContestStartTimeUpdatedConsumerHandlerTests()
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(x => x.UtcNow()).Returns(FixedNow);
        Mocker.Use(dateTimeProvider.Object);

        var appMode = new Mock<IAppMode>();
        appMode.Setup(x => x.CurrentSport).Returns(Sport.FootballNcaa);
        Mocker.Use(appMode.Object);

        Mocker.Use(new Mock<IProvideBackgroundJobs>().Object);

        // Real scheduler so the handler -> scheduler -> DB chain executes end-to-end.
        var scheduler = Mocker.CreateInstance<CompetitionStreamScheduler>();
        Mocker.Use(scheduler);

        _sut = Mocker.CreateInstance<ContestStartTimeUpdatedConsumerHandler>();
    }

    [Fact]
    public async Task Process_WhenStreamRowExistsWithStaleTime_ReschedulesHangfireJob()
    {
        // End-to-end happy path: ESPN moved the game earlier, the existing
        // CompetitionStream points at a Hangfire job at the old time.
        // Handler invokes the scheduler, which cancels the old job and
        // schedules a new one at the new time.

        var competitionId = await SeedCompetitionAsync(competitionDate: FixedNow.AddHours(3));
        var contestId = FootballDataContext.Competitions.Single(c => c.Id == competitionId).ContestId;
        var oldScheduledTime = FixedNow.AddHours(6) - TimeSpan.FromMinutes(10);
        await SeedExistingStreamAsync(competitionId, oldScheduledTime, "hf-old");

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Setup(x => x.Delete("hf-old"))
            .Returns(true);
        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Setup(x => x.Schedule<ICompetitionBroadcastingJob>(
                It.IsAny<Expression<Func<ICompetitionBroadcastingJob, Task>>>(),
                It.IsAny<TimeSpan>()))
            .Returns("hf-new");

        await _sut.Process(BuildEvent(contestId));

        var stream = FootballDataContext.CompetitionStreams.Single();
        stream.BackgroundJobId.Should().Be("hf-new");
        stream.ScheduledTimeUtc.Should().Be(FixedNow.AddHours(3) - TimeSpan.FromMinutes(10));

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Delete("hf-old"), Times.Once);
    }

    [Fact]
    public async Task Process_WhenContestUnknown_DoesNotThrowAndDoesNotTouchHangfire()
    {
        // Event for a contest we don't have locally yet. The scheduler returns
        // false; handler logs and completes. Critical: must not throw — Hangfire
        // would retry forever on a permanent miss.

        await _sut.Process(BuildEvent(Guid.NewGuid()));

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Schedule<ICompetitionBroadcastingJob>(
                It.IsAny<Expression<Func<ICompetitionBroadcastingJob, Task>>>(),
                It.IsAny<TimeSpan>()),
                Times.Never);
        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Process_IgnoresNewStartTimeOnEvent_AndReadsCompetitionDateFromDb()
    {
        // Out-of-order delivery guard: scheduler reads Competition.Date from
        // the DB, not the event payload. Stale event payloads (e.g., a re-delivery
        // after a newer update already landed) must not roll the schedule back.
        // Here the DB says the game is at +3h; we pass an event with NewStartTime
        // at +9h. Result must match the DB, not the event.

        var competitionId = await SeedCompetitionAsync(competitionDate: FixedNow.AddHours(3));
        var contestId = FootballDataContext.Competitions.Single(c => c.Id == competitionId).ContestId;
        var oldScheduledTime = FixedNow.AddHours(6) - TimeSpan.FromMinutes(10);
        await SeedExistingStreamAsync(competitionId, oldScheduledTime, "hf-old");

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Setup(x => x.Delete("hf-old"))
            .Returns(true);
        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Setup(x => x.Schedule<ICompetitionBroadcastingJob>(
                It.IsAny<Expression<Func<ICompetitionBroadcastingJob, Task>>>(),
                It.IsAny<TimeSpan>()))
            .Returns("hf-new");

        var staleEvent = BuildEvent(contestId, newStartTime: FixedNow.AddHours(9));

        await _sut.Process(staleEvent);

        FootballDataContext.CompetitionStreams.Single()
            .ScheduledTimeUtc.Should().Be(FixedNow.AddHours(3) - TimeSpan.FromMinutes(10));
    }

    private async Task<Guid> SeedCompetitionAsync(DateTime competitionDate)
    {
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();

        var contest = new FootballContest
        {
            Id = contestId,
            CreatedBy = Guid.Empty,
            CreatedUtc = FixedNow.AddDays(-7),
            Sport = Sport.FootballNcaa,
            SeasonYear = 2026,
            SeasonWeekId = SeasonWeekId,
            Name = "Test Contest",
            ShortName = "TST",
            StartDateUtc = competitionDate,
        };

        var competition = new FootballCompetition
        {
            Id = competitionId,
            ContestId = contestId,
            Contest = contest,
            CreatedBy = Guid.Empty,
            CreatedUtc = FixedNow.AddDays(-7),
            Date = competitionDate,
        };

        FootballDataContext.Contests.Add(contest);
        FootballDataContext.Competitions.Add(competition);
        await FootballDataContext.SaveChangesAsync();

        return competitionId;
    }

    private async Task SeedExistingStreamAsync(
        Guid competitionId,
        DateTime scheduledTimeUtc,
        string backgroundJobId)
    {
        FootballDataContext.CompetitionStreams.Add(new CompetitionStream
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            SeasonWeekId = SeasonWeekId,
            BackgroundJobId = backgroundJobId,
            ScheduledBy = nameof(CompetitionStreamScheduler),
            ScheduledTimeUtc = scheduledTimeUtc,
            Status = CompetitionStreamStatus.Scheduled,
            CreatedBy = Guid.Empty,
            CreatedUtc = FixedNow.AddDays(-1),
        });
        await FootballDataContext.SaveChangesAsync();
    }

    private static ContestStartTimeUpdated BuildEvent(Guid contestId, DateTime? newStartTime = null)
    {
        return new ContestStartTimeUpdated(
            ContestId: contestId,
            NewStartTime: newStartTime ?? FixedNow.AddHours(3),
            Ref: null,
            Sport: Sport.FootballNcaa,
            SeasonYear: 2026,
            CorrelationId: Guid.NewGuid(),
            CausationId: Guid.NewGuid());
    }
}
