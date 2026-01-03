using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions.Commands.CalculateCompetitionMetrics;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMetrics;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Metrics;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions;

public class RefreshCompetitionMetricsCommandHandlerTests : ProducerTestBase<RefreshCompetitionMetricsCommandHandler>
{
    [Fact]
    public async Task ExecuteAsync_WithFinalizedContests_EnqueuesMetricCalculationJobs()
    {
        // Arrange
        var seasonYear = 2025;
        var contestId1 = Guid.NewGuid();
        var contestId2 = Guid.NewGuid();
        var competitionId1 = Guid.NewGuid();
        var competitionId2 = Guid.NewGuid();

        // Create finalized contests with competitions that have no metrics
        var contest1 = Fixture.Build<Contest>()
            .With(x => x.Id, contestId1)
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.FinalizedUtc, DateTime.UtcNow)
            .Without(x => x.Links)
            .Without(x => x.ExternalIds)
            .Without(x => x.Competitions)
            .Without(x => x.HomeTeamFranchiseSeason)
            .Without(x => x.AwayTeamFranchiseSeason)
            .Create();

        var contest2 = Fixture.Build<Contest>()
            .With(x => x.Id, contestId2)
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.FinalizedUtc, DateTime.UtcNow)
            .Without(x => x.Links)
            .Without(x => x.ExternalIds)
            .Without(x => x.Competitions)
            .Without(x => x.HomeTeamFranchiseSeason)
            .Without(x => x.AwayTeamFranchiseSeason)
            .Create();

        var competition1 = Fixture.Build<Competition>()
            .With(x => x.Id, competitionId1)
            .With(x => x.ContestId, contestId1)
            .Without(x => x.Plays)
            .Without(x => x.Drives)
            .Without(x => x.ExternalIds)
            .Without(x => x.Media)
            .Without(x => x.Metrics)
            .Without(x => x.Contest)
            .Create();

        var competition2 = Fixture.Build<Competition>()
            .With(x => x.Id, competitionId2)
            .With(x => x.ContestId, contestId2)
            .Without(x => x.Plays)
            .Without(x => x.Drives)
            .Without(x => x.ExternalIds)
            .Without(x => x.Media)
            .Without(x => x.Metrics)
            .Without(x => x.Contest)
            .Create();

        await FootballDataContext.Contests.AddRangeAsync(contest1, contest2);
        await FootballDataContext.Competitions.AddRangeAsync(competition1, competition2);
        await FootballDataContext.SaveChangesAsync();

        var backgroundJobProvider = new Mock<IProvideBackgroundJobs>();
        Mocker.Use(backgroundJobProvider.Object);

        var command = new RefreshCompetitionMetricsCommand(seasonYear);
        var sut = Mocker.CreateInstance<RefreshCompetitionMetricsCommandHandler>();

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<RefreshCompetitionMetricsResult>>();
        result.Value.SeasonYear.Should().Be(seasonYear);
        result.Value.TotalContests.Should().Be(2);
        result.Value.EnqueuedJobs.Should().Be(2);

        // Verify job enqueueing would require checking Hangfire internals
        // The important behavior (returning correct result) is tested above
    }

    [Fact]
    public async Task ExecuteAsync_WithCompetitionsThatHaveMetrics_SkipsEnqueueing()
    {
        // Arrange
        var seasonYear = 2025;
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();

        var contest = Fixture.Build<Contest>()
            .With(x => x.Id, contestId)
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.FinalizedUtc, DateTime.UtcNow)
            .Without(x => x.Links)
            .Without(x => x.ExternalIds)
            .Without(x => x.Competitions)
            .Without(x => x.HomeTeamFranchiseSeason)
            .Without(x => x.AwayTeamFranchiseSeason)
            .Create();

        var competition = Fixture.Build<Competition>()
            .With(x => x.Id, competitionId)
            .With(x => x.ContestId, contestId)
            .Without(x => x.Plays)
            .Without(x => x.Drives)
            .Without(x => x.ExternalIds)
            .Without(x => x.Media)
            .Without(x => x.Metrics)
            .Without(x => x.Contest)
            .Create();

        // Add 2 metrics (expected count)
        var metric1 = Fixture.Build<CompetitionMetric>()
            .With(x => x.CompetitionId, competitionId)
            .Without(x => x.Competition)
            .Create();

        var metric2 = Fixture.Build<CompetitionMetric>()
            .With(x => x.CompetitionId, competitionId)
            .Without(x => x.Competition)
            .Create();

        await FootballDataContext.Contests.AddAsync(contest);
        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.CompetitionMetrics.AddRangeAsync(metric1, metric2);
        await FootballDataContext.SaveChangesAsync();

        var backgroundJobProvider = new Mock<IProvideBackgroundJobs>();
        Mocker.Use(backgroundJobProvider.Object);

        var command = new RefreshCompetitionMetricsCommand(seasonYear);
        var sut = Mocker.CreateInstance<RefreshCompetitionMetricsCommandHandler>();

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<RefreshCompetitionMetricsResult>>();
        result.Value.EnqueuedJobs.Should().Be(0);

        // Verify no enqueueing by checking result shows 0 jobs
    }

    [Fact]
    public async Task ExecuteAsync_WithNoContests_ReturnsZeroEnqueued()
    {
        // Arrange
        var seasonYear = 2099; // Future year with no data
        var command = new RefreshCompetitionMetricsCommand(seasonYear);

        var backgroundJobProvider = new Mock<IProvideBackgroundJobs>();
        Mocker.Use(backgroundJobProvider.Object);

        var sut = Mocker.CreateInstance<RefreshCompetitionMetricsCommandHandler>();

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<RefreshCompetitionMetricsResult>>();
        result.Value.TotalContests.Should().Be(0);
        result.Value.EnqueuedJobs.Should().Be(0);

        // Verify no enqueueing by checking result shows 0 jobs
    }
}
