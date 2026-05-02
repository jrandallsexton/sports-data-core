using System.Linq.Expressions;

using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions;

/// <summary>
/// Covers the three scheduler decisions: insert (no row yet), reschedule
/// (Scheduled row + drift > threshold), and skip (no row needed because
/// already-correct, terminal-state, or about-to-fire).
/// </summary>
public class CompetitionStreamSchedulerTests : ProducerTestBase<CompetitionStreamScheduler>
{
    private static readonly DateTime FixedNow = new(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SeasonWeekId = Guid.NewGuid();

    private readonly CompetitionStreamScheduler _sut;

    public CompetitionStreamSchedulerTests()
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(x => x.UtcNow()).Returns(FixedNow);
        Mocker.Use(dateTimeProvider.Object);

        var appMode = new Mock<IAppMode>();
        appMode.Setup(x => x.CurrentSport).Returns(Sport.FootballNcaa);
        Mocker.Use(appMode.Object);

        Mocker.Use(new Mock<IProvideBackgroundJobs>().Object);

        _sut = Mocker.CreateInstance<CompetitionStreamScheduler>();
    }

    [Fact]
    public async Task Execute_WhenNoCurrentSeasonWeek_LogsAndReturns()
    {
        // SeasonWeek table empty → guard logs and returns without scheduling anything.

        await _sut.ExecuteAsync(CancellationToken.None);

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Schedule<ICompetitionBroadcastingJob>(
                It.IsAny<Expression<Func<ICompetitionBroadcastingJob, Task>>>(),
                It.IsAny<TimeSpan>()),
                Times.Never);
    }

    [Fact]
    public async Task Execute_NoExistingStream_SchedulesNewJobAndInsertsRow()
    {
        await SeedSeasonWeekAsync();
        var competitionId = await SeedCompetitionAsync(competitionDate: FixedNow.AddHours(3));

        SetupScheduleReturns("hf-new-1");

        await _sut.ExecuteAsync(CancellationToken.None);

        var stream = FootballDataContext.CompetitionStreams.Single();
        stream.CompetitionId.Should().Be(competitionId);
        stream.BackgroundJobId.Should().Be("hf-new-1");
        stream.Status.Should().Be(CompetitionStreamStatus.Scheduled);
        stream.ScheduledTimeUtc.Should().Be(FixedNow.AddHours(3) - TimeSpan.FromMinutes(10));

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_NoEspnExternalId_LogsAndSkips()
    {
        await SeedSeasonWeekAsync();
        await SeedCompetitionAsync(competitionDate: FixedNow.AddHours(3), withEspnExternalId: false);

        await _sut.ExecuteAsync(CancellationToken.None);

        FootballDataContext.CompetitionStreams.Should().BeEmpty();

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Schedule<ICompetitionBroadcastingJob>(
                It.IsAny<Expression<Func<ICompetitionBroadcastingJob, Task>>>(),
                It.IsAny<TimeSpan>()),
                Times.Never);
    }

    [Fact]
    public async Task Execute_ExistingScheduledStream_NoDrift_DoesNothing()
    {
        // Existing stream's ScheduledTimeUtc matches what the scheduler would compute.
        // No reschedule, no new schedule.

        await SeedSeasonWeekAsync();
        var competitionId = await SeedCompetitionAsync(competitionDate: FixedNow.AddHours(3));
        var existingScheduledTime = FixedNow.AddHours(3) - TimeSpan.FromMinutes(10);
        await SeedExistingStreamAsync(competitionId, existingScheduledTime, CompetitionStreamStatus.Scheduled, "hf-existing");

        await _sut.ExecuteAsync(CancellationToken.None);

        FootballDataContext.CompetitionStreams.Single().BackgroundJobId.Should().Be("hf-existing");

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Schedule<ICompetitionBroadcastingJob>(
                It.IsAny<Expression<Func<ICompetitionBroadcastingJob, Task>>>(),
                It.IsAny<TimeSpan>()),
                Times.Never);
    }

    [Fact]
    public async Task Execute_ExistingScheduledStream_DriftWithinThreshold_DoesNothing()
    {
        // Game shifted by 4 minutes — within the 5-minute threshold. Treat as noise; no reschedule.

        await SeedSeasonWeekAsync();
        var competitionId = await SeedCompetitionAsync(competitionDate: FixedNow.AddHours(3));
        var staleScheduledTime = (FixedNow.AddHours(3) - TimeSpan.FromMinutes(10)) + TimeSpan.FromMinutes(4);
        await SeedExistingStreamAsync(competitionId, staleScheduledTime, CompetitionStreamStatus.Scheduled, "hf-existing");

        await _sut.ExecuteAsync(CancellationToken.None);

        FootballDataContext.CompetitionStreams.Single().BackgroundJobId.Should().Be("hf-existing");

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_ExistingScheduledStream_GameMovedLater_ReschedulesJob()
    {
        // Originally scheduled for FixedNow+3h; ESPN moves the game to FixedNow+5h.
        // Drift = 2h, well above threshold. Old job deleted, new job scheduled, row updated.

        await SeedSeasonWeekAsync();
        var competitionId = await SeedCompetitionAsync(competitionDate: FixedNow.AddHours(5));
        var oldScheduledTime = FixedNow.AddHours(3) - TimeSpan.FromMinutes(10);
        await SeedExistingStreamAsync(competitionId, oldScheduledTime, CompetitionStreamStatus.Scheduled, "hf-old");

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Setup(x => x.Delete("hf-old"))
            .Returns(true);
        SetupScheduleReturns("hf-new-2");

        await _sut.ExecuteAsync(CancellationToken.None);

        var stream = FootballDataContext.CompetitionStreams.Single();
        stream.BackgroundJobId.Should().Be("hf-new-2");
        stream.ScheduledTimeUtc.Should().Be(FixedNow.AddHours(5) - TimeSpan.FromMinutes(10));
        stream.Notes.Should().Contain("Rescheduled");
        stream.Notes.Should().Contain("hf-old");

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Delete("hf-old"), Times.Once);
    }

    [Fact]
    public async Task Execute_ExistingScheduledStream_GameMovedEarlier_ReschedulesJob()
    {
        // Originally scheduled for FixedNow+5h; ESPN moves the game to FixedNow+3h.
        // Same drift, opposite direction. Same outcome: reschedule.

        await SeedSeasonWeekAsync();
        var competitionId = await SeedCompetitionAsync(competitionDate: FixedNow.AddHours(3));
        var oldScheduledTime = FixedNow.AddHours(5) - TimeSpan.FromMinutes(10);
        await SeedExistingStreamAsync(competitionId, oldScheduledTime, CompetitionStreamStatus.Scheduled, "hf-old");

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Setup(x => x.Delete("hf-old"))
            .Returns(true);
        SetupScheduleReturns("hf-new-3");

        await _sut.ExecuteAsync(CancellationToken.None);

        var stream = FootballDataContext.CompetitionStreams.Single();
        stream.BackgroundJobId.Should().Be("hf-new-3");
        stream.ScheduledTimeUtc.Should().Be(FixedNow.AddHours(3) - TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task Execute_ExistingScheduledStream_AboutToFire_DoesNotReschedule()
    {
        // Existing job fires in 30 seconds; even if drift is large, don't race the Hangfire scheduler.

        await SeedSeasonWeekAsync();
        var competitionId = await SeedCompetitionAsync(competitionDate: FixedNow.AddHours(5));
        var oldScheduledTime = FixedNow.AddSeconds(30);
        await SeedExistingStreamAsync(competitionId, oldScheduledTime, CompetitionStreamStatus.Scheduled, "hf-imminent");

        await _sut.ExecuteAsync(CancellationToken.None);

        FootballDataContext.CompetitionStreams.Single().BackgroundJobId.Should().Be("hf-imminent");

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(CompetitionStreamStatus.AwaitingStart)]
    [InlineData(CompetitionStreamStatus.Active)]
    [InlineData(CompetitionStreamStatus.Completed)]
    [InlineData(CompetitionStreamStatus.Failed)]
    public async Task Execute_NonScheduledStatus_DoesNotTouchStream(CompetitionStreamStatus status)
    {
        // Live or terminal streams must not be disturbed. The streamer's own
        // status polling owns the lifecycle from AwaitingStart onward.

        await SeedSeasonWeekAsync();
        var competitionId = await SeedCompetitionAsync(competitionDate: FixedNow.AddHours(5));
        var staleScheduledTime = FixedNow.AddHours(3) - TimeSpan.FromMinutes(10);
        await SeedExistingStreamAsync(competitionId, staleScheduledTime, status, "hf-untouchable");

        await _sut.ExecuteAsync(CancellationToken.None);

        FootballDataContext.CompetitionStreams.Single().BackgroundJobId.Should().Be("hf-untouchable");

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Schedule<ICompetitionBroadcastingJob>(
                It.IsAny<Expression<Func<ICompetitionBroadcastingJob, Task>>>(),
                It.IsAny<TimeSpan>()),
                Times.Never);
    }

    [Fact]
    public async Task Execute_DeleteReturnsFalse_KeepsExistingRowUnchanged()
    {
        // Hangfire refused the state transition (job already Processing/Succeeded/Deleted).
        // Scheduler must not blindly schedule a new job in that case — the row stays intact.

        await SeedSeasonWeekAsync();
        var competitionId = await SeedCompetitionAsync(competitionDate: FixedNow.AddHours(5));
        var oldScheduledTime = FixedNow.AddHours(3) - TimeSpan.FromMinutes(10);
        await SeedExistingStreamAsync(competitionId, oldScheduledTime, CompetitionStreamStatus.Scheduled, "hf-locked");

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Setup(x => x.Delete("hf-locked"))
            .Returns(false);

        await _sut.ExecuteAsync(CancellationToken.None);

        var stream = FootballDataContext.CompetitionStreams.Single();
        stream.BackgroundJobId.Should().Be("hf-locked");
        stream.ScheduledTimeUtc.Should().Be(oldScheduledTime);

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Schedule<ICompetitionBroadcastingJob>(
                It.IsAny<Expression<Func<ICompetitionBroadcastingJob, Task>>>(),
                It.IsAny<TimeSpan>()),
                Times.Never);
    }

    private async Task SeedSeasonWeekAsync()
    {
        FootballDataContext.SeasonWeeks.Add(new SeasonWeek
        {
            Id = SeasonWeekId,
            CreatedBy = Guid.Empty,
            CreatedUtc = FixedNow.AddDays(-1),
            Number = 5,
            StartDate = FixedNow.AddDays(-1),
            EndDate = FixedNow.AddDays(6),
            SeasonId = Guid.NewGuid(),
        });
        await FootballDataContext.SaveChangesAsync();
    }

    private async Task<Guid> SeedCompetitionAsync(DateTime competitionDate, bool withEspnExternalId = true)
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
            ExternalIds = withEspnExternalId
                ? new List<CompetitionExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        CompetitionId = competitionId,
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/1/competitions/" + competitionId,
                        SourceUrlHash = competitionId.ToString("N"),
                        Value = competitionId.ToString(),
                        CreatedBy = Guid.Empty,
                        CreatedUtc = FixedNow.AddDays(-7),
                    }
                }
                : new List<CompetitionExternalId>(),
        };

        FootballDataContext.Contests.Add(contest);
        FootballDataContext.Competitions.Add(competition);
        await FootballDataContext.SaveChangesAsync();

        return competitionId;
    }

    private async Task SeedExistingStreamAsync(
        Guid competitionId,
        DateTime scheduledTimeUtc,
        CompetitionStreamStatus status,
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
            Status = status,
            CreatedBy = Guid.Empty,
            CreatedUtc = FixedNow.AddDays(-1),
        });
        await FootballDataContext.SaveChangesAsync();
    }

    private void SetupScheduleReturns(string jobId)
    {
        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Setup(x => x.Schedule<ICompetitionBroadcastingJob>(
                It.IsAny<Expression<Func<ICompetitionBroadcastingJob, Task>>>(),
                It.IsAny<TimeSpan>()))
            .Returns(jobId);
    }
}
