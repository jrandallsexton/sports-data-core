using System.Linq.Expressions;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Consumers;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Consumers;

public class CompetitorScoreUpdatedConsumerHandlerTests
    : ProducerTestBase<CompetitorScoreUpdatedConsumerHandler>
{
    private static readonly Guid HomeFranchiseSeasonId = Guid.NewGuid();
    private static readonly Guid AwayFranchiseSeasonId = Guid.NewGuid();

    private readonly CompetitorScoreUpdatedConsumerHandler _sut;

    public CompetitorScoreUpdatedConsumerHandlerTests()
    {
        _sut = Mocker.CreateInstance<CompetitorScoreUpdatedConsumerHandler>();
    }

    [Fact]
    public async Task Process_WhenContestNotFound_DoesNotPublishOrSave()
    {
        var evt = BuildEvent(contestId: Guid.NewGuid(), franchiseSeasonId: HomeFranchiseSeasonId, score: 7);

        await _sut.Process(evt);

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestScoreChanged>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenFranchiseSeasonIsHome_UpdatesHomeScoreAndPublishes()
    {
        var contestId = Guid.NewGuid();
        await SeedContest(contestId);

        var evt = BuildEvent(contestId, franchiseSeasonId: HomeFranchiseSeasonId, score: 14);

        await _sut.Process(evt);

        var saved = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        saved.HomeScore.Should().Be(14);
        saved.AwayScore.Should().BeNull();

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(
                It.Is<ContestScoreChanged>(e =>
                    e.ContestId == contestId &&
                    e.FranchiseSeasonId == HomeFranchiseSeasonId &&
                    e.Score == 14),
                It.IsAny<CancellationToken>()),
                Times.Once);
    }

    [Fact]
    public async Task Process_WhenFranchiseSeasonIsAway_UpdatesAwayScoreAndPublishes()
    {
        var contestId = Guid.NewGuid();
        await SeedContest(contestId);

        var evt = BuildEvent(contestId, franchiseSeasonId: AwayFranchiseSeasonId, score: 21);

        await _sut.Process(evt);

        var saved = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        saved.AwayScore.Should().Be(21);
        saved.HomeScore.Should().BeNull();

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(
                It.Is<ContestScoreChanged>(e =>
                    e.ContestId == contestId &&
                    e.FranchiseSeasonId == AwayFranchiseSeasonId &&
                    e.Score == 21),
                It.IsAny<CancellationToken>()),
                Times.Once);
    }

    [Fact]
    public async Task Process_WhenFranchiseSeasonMatchesNeitherTeam_DoesNotUpdateOrPublish()
    {
        var contestId = Guid.NewGuid();
        await SeedContest(contestId);

        var unrelatedFranchiseSeasonId = Guid.NewGuid();
        var evt = BuildEvent(contestId, franchiseSeasonId: unrelatedFranchiseSeasonId, score: 99);

        await _sut.Process(evt);

        var saved = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        saved.HomeScore.Should().BeNull();
        saved.AwayScore.Should().BeNull();

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestScoreChanged>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenScoreUnchanged_NoOpsAndDoesNotPublish()
    {
        // At-least-once redelivery: the upstream score-doc processor only
        // publishes when the persisted score actually changes, but MassTransit /
        // broker retry can still re-deliver the same event. Without the guard,
        // each redelivery would re-stamp ModifiedUtc and re-broadcast
        // ContestScoreChanged to SignalR clients.
        var contestId = Guid.NewGuid();
        await SeedContest(contestId, homeScore: 14);

        var evt = BuildEvent(contestId, franchiseSeasonId: HomeFranchiseSeasonId, score: 14);

        await _sut.Process(evt);

        var saved = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        saved.HomeScore.Should().Be(14);
        saved.ModifiedUtc.Should().BeNull();

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(It.IsAny<ContestScoreChanged>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_StampsModifiedByWithCorrelationIdAndModifiedUtc()
    {
        var contestId = Guid.NewGuid();
        await SeedContest(contestId);

        var correlationId = Guid.NewGuid();
        var evt = BuildEvent(contestId, franchiseSeasonId: HomeFranchiseSeasonId, score: 3, correlationId: correlationId);

        var fixedUtc = new DateTime(2026, 5, 1, 14, 30, 0, DateTimeKind.Utc);
        Mock.Get(Mocker.Get<IDateTimeProvider>())
            .Setup(x => x.UtcNow())
            .Returns(fixedUtc);

        await _sut.Process(evt);

        var saved = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        saved.ModifiedBy.Should().Be(correlationId);
        saved.ModifiedUtc.Should().Be(fixedUtc);
    }

    [Fact]
    public async Task Process_PublishedEvent_PreservesCorrelationIdAndSportAndSeasonYear()
    {
        var contestId = Guid.NewGuid();
        await SeedContest(contestId);

        var correlationId = Guid.NewGuid();
        var evt = BuildEvent(
            contestId,
            franchiseSeasonId: HomeFranchiseSeasonId,
            score: 10,
            correlationId: correlationId,
            sport: Sport.BaseballMlb,
            seasonYear: 2026);

        await _sut.Process(evt);

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(
                It.Is<ContestScoreChanged>(e =>
                    e.CorrelationId == correlationId &&
                    e.Sport == Sport.BaseballMlb &&
                    e.SeasonYear == 2026 &&
                    e.CausationId == CausationId.Producer.EventCompetitionCompetitorScoreDocumentProcessor),
                It.IsAny<CancellationToken>()),
                Times.Once);
    }

    [Fact]
    public async Task Process_WhenContestFinalAndFinalizedUtcIsNull_ReEnqueuesEnrichContestCommand()
    {
        // Stuck-Final recovery: ContestEnrichment ran early (e.g. saw 0-0,
        // deferred), then scores arrived later. Score handler should re-kick
        // enrichment so the deferred contest finalizes against the now-correct
        // DB state. See 2026-06-24 MLB stuck-Final incident.
        var contestId = Guid.NewGuid();
        await SeedContest(contestId);
        await SeedCompetitionWithStatus(contestId, statusTypeName: "STATUS_FINAL");

        var evt = BuildEvent(contestId, franchiseSeasonId: HomeFranchiseSeasonId, score: 4);

        await _sut.Process(evt);

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Enqueue<IEnrichContests>(
                It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
                Times.Once);
    }

    [Fact]
    public async Task Process_WhenContestAlreadyFinalized_DoesNotReEnqueueEnrichContestCommand()
    {
        // FinalizedUtc is already set — enrichment ran to completion. No
        // reason to re-fire it on subsequent score-doc redelivery / late
        // odds-only score writes.
        var contestId = Guid.NewGuid();
        await SeedContest(contestId, finalizedUtc: new DateTime(2026, 6, 24, 23, 0, 0, DateTimeKind.Utc));
        await SeedCompetitionWithStatus(contestId, statusTypeName: "STATUS_FINAL");

        var evt = BuildEvent(contestId, franchiseSeasonId: HomeFranchiseSeasonId, score: 4);

        await _sut.Process(evt);

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Enqueue<IEnrichContests>(
                It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
                Times.Never);
    }

    [Fact]
    public async Task Process_WhenStatusIsLiveAndNotFinalized_DoesNotReEnqueueEnrichContestCommand()
    {
        // Steady-state live-game score update — status is STATUS_IN_PROGRESS
        // (not FINAL), FinalizedUtc is null. Re-kicking enrichment here would
        // churn the entire game.
        var contestId = Guid.NewGuid();
        await SeedContest(contestId);
        await SeedCompetitionWithStatus(contestId, statusTypeName: "STATUS_IN_PROGRESS");

        var evt = BuildEvent(contestId, franchiseSeasonId: HomeFranchiseSeasonId, score: 7);

        await _sut.Process(evt);

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Enqueue<IEnrichContests>(
                It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
                Times.Never);
    }

    private async Task SeedContest(Guid contestId, int? homeScore = null, int? awayScore = null, DateTime? finalizedUtc = null)
    {
        FootballDataContext.Contests.Add(new FootballContest
        {
            Id = contestId,
            Name = "Test Game",
            ShortName = "TST",
            StartDateUtc = DateTime.UtcNow,
            Sport = Sport.FootballNcaa,
            SeasonYear = 2026,
            HomeTeamFranchiseSeasonId = HomeFranchiseSeasonId,
            AwayTeamFranchiseSeasonId = AwayFranchiseSeasonId,
            HomeScore = homeScore,
            AwayScore = awayScore,
            FinalizedUtc = finalizedUtc
        });
        await FootballDataContext.SaveChangesAsync();
    }

    private async Task SeedCompetitionWithStatus(Guid contestId, string statusTypeName)
    {
        var competitionId = Guid.NewGuid();
        FootballDataContext.Competitions.Add(new FootballCompetition
        {
            Id = competitionId,
            ContestId = contestId,
            Date = DateTime.UtcNow,
            CreatedBy = Guid.Empty,
            CreatedUtc = DateTime.UtcNow
        });
        FootballDataContext.CompetitionStatuses.Add(new FootballCompetitionStatus
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            StatusTypeName = statusTypeName,
            StatusTypeId = "0",
            StatusState = "post",
            StatusDescription = statusTypeName,
            StatusDetail = statusTypeName,
            CreatedBy = Guid.Empty,
            CreatedUtc = DateTime.UtcNow
        });
        await FootballDataContext.SaveChangesAsync();
    }

    private static CompetitorScoreUpdated BuildEvent(
        Guid contestId,
        Guid franchiseSeasonId,
        int score,
        Guid? correlationId = null,
        Sport sport = Sport.FootballNcaa,
        int? seasonYear = 2026)
    {
        return new CompetitorScoreUpdated(
            ContestId: contestId,
            FranchiseSeasonId: franchiseSeasonId,
            Score: score,
            Ref: null,
            Sport: sport,
            SeasonYear: seasonYear,
            CorrelationId: correlationId ?? Guid.NewGuid(),
            CausationId: CausationId.Producer.EventCompetitionCompetitorScoreDocumentProcessor);
    }
}
