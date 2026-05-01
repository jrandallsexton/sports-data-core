using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Producer.Application.Consumers;
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
    public async Task Process_StampsModifiedByWithCorrelationIdAndModifiedUtc()
    {
        var contestId = Guid.NewGuid();
        await SeedContest(contestId);

        var correlationId = Guid.NewGuid();
        var evt = BuildEvent(contestId, franchiseSeasonId: HomeFranchiseSeasonId, score: 3, correlationId: correlationId);

        var beforeUtc = DateTime.UtcNow.AddSeconds(-1);

        await _sut.Process(evt);

        var saved = await FootballDataContext.Contests.AsNoTracking().FirstAsync(c => c.Id == contestId);
        saved.ModifiedBy.Should().Be(correlationId);
        saved.ModifiedUtc.Should().NotBeNull();
        saved.ModifiedUtc!.Value.Should().BeOnOrAfter(beforeUtc);
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

    private async Task SeedContest(Guid contestId)
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
            AwayTeamFranchiseSeasonId = AwayFranchiseSeasonId
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
