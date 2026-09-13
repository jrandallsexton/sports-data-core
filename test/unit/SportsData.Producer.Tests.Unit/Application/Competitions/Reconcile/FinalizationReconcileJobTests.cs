#nullable enable

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;
using Moq.Protected;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Application.Competitions.Reconcile;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using System.Net;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions.Reconcile;

public class FinalizationReconcileJobTests : ProducerTestBase<FinalizationReconcileJob<FootballDataContext>>
{
    private static readonly DateTime FixedNow = new(2026, 6, 14, 12, 0, 0, DateTimeKind.Utc);

    private const string FinalStatusJson = """
    { "type": { "name": "STATUS_FINAL" } }
    """;

    private const string InProgressStatusJson = """
    { "type": { "name": "STATUS_IN_PROGRESS" } }
    """;

    private void SetFixedTime()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(p => p.UtcNow())
            .Returns(FixedNow);
    }

    private void SetSport(Sport sport)
    {
        Mocker.GetMock<IAppMode>()
            .SetupGet(m => m.CurrentSport)
            .Returns(sport);
    }

    private Mock<IMessageDeliveryScope> SetupDeliveryScopeNoop()
    {
        var scope = Mocker.GetMock<IMessageDeliveryScope>();
        scope.Setup(s => s.Use(It.IsAny<DeliveryMode>())).Returns(Mock.Of<IDisposable>());
        return scope;
    }

    private void SetupEspnHttp(HttpStatusCode status, string? body)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(body ?? string.Empty)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        Mocker.Use(factory.Object);
    }

    private async Task<(FootballContest contest, FootballCompetition competition, CompetitionStream stream)>
        SeedStrandedAsync(
            DateTime streamStartedUtc,
            CompetitionStreamStatus status = CompetitionStreamStatus.Failed,
            bool contestFinalized = false,
        bool neverStarted = false,
        DateTime? scheduledTimeUtc = null)
    {
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();

        var contest = new FootballContest
        {
            Id = contestId,
            Name = "Test Game",
            ShortName = "TG",
            SeasonYear = 2025,
            Sport = Sport.FootballNcaa,
            StartDateUtc = streamStartedUtc,
            HomeTeamFranchiseSeasonId = Guid.NewGuid(),
            AwayTeamFranchiseSeasonId = Guid.NewGuid(),
            FinalizedUtc = contestFinalized ? FixedNow.AddHours(-1) : null,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };

        var competition = new FootballCompetition
        {
            Id = competitionId,
            ContestId = contest.Id,
            Contest = contest,
            Date = streamStartedUtc,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<CompetitionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/1/competitions/1",
                    SourceUrlHash = "test-hash",
                    Value = "1",
                    CreatedUtc = FixedNow,
                    CreatedBy = Guid.NewGuid()
                }
            }
        };

        var stream = new CompetitionStream
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            Competition = competition,
            SeasonWeekId = Guid.NewGuid(),
            ScheduledTimeUtc = scheduledTimeUtc ?? streamStartedUtc,
            BackgroundJobId = "test-job",
            Status = status,
            StreamStartedUtc = neverStarted ? null : streamStartedUtc,
            FailureReason = status == CompetitionStreamStatus.Failed ? "Cancelled by external request" : null,
            RetryCount = 0,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.Contests.AddAsync(contest);
        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.CompetitionStreams.AddAsync(stream);
        await FootballDataContext.SaveChangesAsync();
        FootballDataContext.ChangeTracker.Clear();

        return (contest, competition, stream);
    }

    [Fact]
    public async Task ExecuteAsync_NoStreams_CompletesWithoutError()
    {
        SetFixedTime();
        SetSport(Sport.FootballNcaa);
        SetupDeliveryScopeNoop();
        SetupEspnHttp(HttpStatusCode.OK, FinalStatusJson);

        var sut = Mocker.CreateInstance<FinalizationReconcileJob<FootballDataContext>>();
        await sut.ExecuteAsync(CancellationToken.None);

        Mocker.GetMock<IEventBus>()
            .Verify(b => b.Publish(It.IsAny<ContestCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_StrandedStreamFinalPerEspn_PublishesEventsAndMarksCompleted()
    {
        SetFixedTime();
        SetSport(Sport.FootballNcaa);
        SetupDeliveryScopeNoop();
        SetupEspnHttp(HttpStatusCode.OK, FinalStatusJson);

        var (contest, _, stream) = await SeedStrandedAsync(
            streamStartedUtc: FixedNow.AddHours(-4),
            status: CompetitionStreamStatus.Failed);

        var sut = Mocker.CreateInstance<FinalizationReconcileJob<FootballDataContext>>();
        await sut.ExecuteAsync(CancellationToken.None);

        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(
                It.Is<ContestCompleted>(e => e.ContestId == contest.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(
                It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.Event),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var refreshed = await FootballDataContext.CompetitionStreams
            .FirstAsync(s => s.Id == stream.Id);
        refreshed.Status.Should().Be(CompetitionStreamStatus.Completed);
        refreshed.StreamEndedUtc.Should().Be(FixedNow);
        refreshed.FailureReason.Should().Be("Cancelled by external request",
            "diagnostic evidence of the original streamer miss is intentionally preserved");
    }

    [Fact]
    public async Task ExecuteAsync_StrandedStreamStillInProgressPerEspn_DoesNothing()
    {
        SetFixedTime();
        SetSport(Sport.FootballNcaa);
        SetupDeliveryScopeNoop();
        SetupEspnHttp(HttpStatusCode.OK, InProgressStatusJson);

        var (contest, _, stream) = await SeedStrandedAsync(
            streamStartedUtc: FixedNow.AddHours(-3),
            status: CompetitionStreamStatus.Failed);

        var sut = Mocker.CreateInstance<FinalizationReconcileJob<FootballDataContext>>();
        await sut.ExecuteAsync(CancellationToken.None);

        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(It.IsAny<ContestCompleted>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var refreshed = await FootballDataContext.CompetitionStreams
            .FirstAsync(s => s.Id == stream.Id);
        refreshed.Status.Should().Be(CompetitionStreamStatus.Failed,
            "stream stays Failed until ESPN reports FINAL");
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyFinalizedContest_IsSkipped()
    {
        SetFixedTime();
        SetSport(Sport.FootballNcaa);
        SetupDeliveryScopeNoop();
        SetupEspnHttp(HttpStatusCode.OK, FinalStatusJson);

        var (_, _, stream) = await SeedStrandedAsync(
            streamStartedUtc: FixedNow.AddHours(-4),
            status: CompetitionStreamStatus.Failed,
            contestFinalized: true);

        var sut = Mocker.CreateInstance<FinalizationReconcileJob<FootballDataContext>>();
        await sut.ExecuteAsync(CancellationToken.None);

        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(It.IsAny<ContestCompleted>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_StreamOlderThan48h_IsOutsideWindowAndSkipped()
    {
        SetFixedTime();
        SetSport(Sport.FootballNcaa);
        SetupDeliveryScopeNoop();
        SetupEspnHttp(HttpStatusCode.OK, FinalStatusJson);

        var (_, _, stream) = await SeedStrandedAsync(
            streamStartedUtc: FixedNow.AddHours(-72),
            status: CompetitionStreamStatus.Failed);

        var sut = Mocker.CreateInstance<FinalizationReconcileJob<FootballDataContext>>();
        await sut.ExecuteAsync(CancellationToken.None);

        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(It.IsAny<ContestCompleted>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// The never-started arm: a stream that died during startup, so
    /// StreamStartedUtc was never set. This is the exact shape of the
    /// 2026-09-13 outage, and before the fix this job could not see it.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NeverStartedStreamFinalPerEspn_PublishesEventsAndMarksCompleted()
    {
        SetFixedTime();
        SetSport(Sport.FootballNcaa);
        SetupDeliveryScopeNoop();
        SetupEspnHttp(HttpStatusCode.OK, FinalStatusJson);

        var (contest, _, stream) = await SeedStrandedAsync(
            streamStartedUtc: FixedNow.AddHours(-4),
            status: CompetitionStreamStatus.Failed,
            neverStarted: true,
            scheduledTimeUtc: FixedNow.AddHours(-4));

        var sut = Mocker.CreateInstance<FinalizationReconcileJob<FootballDataContext>>();

        await sut.ExecuteAsync(CancellationToken.None);

        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(It.IsAny<ContestCompleted>(), It.IsAny<CancellationToken>()),
            Times.Once(),
            "a stream that never attached is exactly what this backstop exists to rescue");

        var refreshed = await FootballDataContext.CompetitionStreams
            .FirstAsync(x => x.Id == stream.Id);
        refreshed.Status.Should().Be(CompetitionStreamStatus.Completed);
    }

    /// <summary>
    /// A stream stranded at AwaitingStart — the status held for the whole
    /// startup window, which the retry in this PR widened to as much as ten
    /// minutes. A pod killed inside it writes no status at all.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NeverStartedStreamStuckAwaitingStart_IsReconciled()
    {
        SetFixedTime();
        SetSport(Sport.FootballNcaa);
        SetupDeliveryScopeNoop();
        SetupEspnHttp(HttpStatusCode.OK, FinalStatusJson);

        var (_, _, stream) = await SeedStrandedAsync(
            streamStartedUtc: FixedNow.AddHours(-4),
            status: CompetitionStreamStatus.AwaitingStart,
            neverStarted: true,
            scheduledTimeUtc: FixedNow.AddHours(-4));

        var sut = Mocker.CreateInstance<FinalizationReconcileJob<FootballDataContext>>();

        await sut.ExecuteAsync(CancellationToken.None);

        var refreshed = await FootballDataContext.CompetitionStreams
            .FirstAsync(x => x.Id == stream.Id);
        refreshed.Status.Should().Be(CompetitionStreamStatus.Completed);
    }

    /// <summary>
    /// The 48h cap applies to the never-started arm too, or a backfill of old
    /// rows would turn every pass into a large ESPN polling bill.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NeverStartedStreamOlderThan48h_IsOutsideWindowAndSkipped()
    {
        SetFixedTime();
        SetSport(Sport.FootballNcaa);
        SetupDeliveryScopeNoop();
        SetupEspnHttp(HttpStatusCode.OK, FinalStatusJson);

        var (_, _, stream) = await SeedStrandedAsync(
            streamStartedUtc: FixedNow.AddHours(-72),
            status: CompetitionStreamStatus.Failed,
            neverStarted: true,
            scheduledTimeUtc: FixedNow.AddHours(-72));

        var sut = Mocker.CreateInstance<FinalizationReconcileJob<FootballDataContext>>();

        await sut.ExecuteAsync(CancellationToken.None);

        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(It.IsAny<ContestCompleted>(), It.IsAny<CancellationToken>()),
            Times.Never());

        var refreshed = await FootballDataContext.CompetitionStreams
            .FirstAsync(x => x.Id == stream.Id);
        refreshed.Status.Should().Be(CompetitionStreamStatus.Failed);
    }

    /// <summary>
    /// A game whose kickoff has not arrived must never be polled, or the lower
    /// bound alone would drag every scheduled game of the next 48h into a pass.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NeverStartedStreamNotYetKickedOff_IsSkipped()
    {
        SetFixedTime();
        SetSport(Sport.FootballNcaa);
        SetupDeliveryScopeNoop();
        SetupEspnHttp(HttpStatusCode.OK, FinalStatusJson);

        var (_, _, stream) = await SeedStrandedAsync(
            streamStartedUtc: FixedNow.AddHours(-4),
            status: CompetitionStreamStatus.AwaitingStart,
            neverStarted: true,
            scheduledTimeUtc: FixedNow.AddHours(3));

        var sut = Mocker.CreateInstance<FinalizationReconcileJob<FootballDataContext>>();

        await sut.ExecuteAsync(CancellationToken.None);

        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(It.IsAny<ContestCompleted>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }
}
