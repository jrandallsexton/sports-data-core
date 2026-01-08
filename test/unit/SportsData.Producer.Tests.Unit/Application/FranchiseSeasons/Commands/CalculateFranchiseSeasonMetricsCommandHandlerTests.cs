using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Producer.Application.FranchiseSeasons.Commands.CalculateFranchiseSeasonMetrics;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Metrics;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseSeasons.Commands;

public class CalculateFranchiseSeasonMetricsCommandHandlerTests :
    ProducerTestBase<CalculateFranchiseSeasonMetricsCommandHandler>
{
    [Fact]
    public async Task WhenCompetitionMetricsExist_ShouldCalculateAndSaveFranchiseSeasonMetric()
    {
        // Arrange
        var sut = Mocker.CreateInstance<CalculateFranchiseSeasonMetricsCommandHandler>();

        var franchiseSeasonId = Guid.NewGuid();
        var seasonYear = 2024;

        // Create contest with competitions
        var contest = CreateContest(franchiseSeasonId, seasonYear);
        await FootballDataContext.Contests.AddAsync(contest);

        var competition = Fixture.Build<Competition>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.ContestId, contest.Id)
            .With(x => x.Contest, contest)
            .Create();

        await FootballDataContext.Competitions.AddAsync(competition);

        // Create competition metrics
        var competitionMetric = CreateCompetitionMetric(competition.Id, franchiseSeasonId, seasonYear);
        await FootballDataContext.CompetitionMetrics.AddAsync(competitionMetric);
        await FootballDataContext.SaveChangesAsync();

        var command = new CalculateFranchiseSeasonMetricsCommand(franchiseSeasonId, seasonYear);

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(franchiseSeasonId);

        var savedMetric = await FootballDataContext.FranchiseSeasonMetrics
            .FirstOrDefaultAsync(x => x.FranchiseSeasonId == franchiseSeasonId);

        savedMetric.Should().NotBeNull();
        savedMetric!.GamesPlayed.Should().Be(1);
        savedMetric.Ypp.Should().Be(5.5m);
        savedMetric.SuccessRate.Should().Be(0.45m);
    }

    [Fact]
    public async Task WhenNoCompetitionMetricsExist_ShouldReturnSuccessWithoutCreatingMetric()
    {
        // Arrange
        var sut = Mocker.CreateInstance<CalculateFranchiseSeasonMetricsCommandHandler>();

        var franchiseSeasonId = Guid.NewGuid();
        var command = new CalculateFranchiseSeasonMetricsCommand(franchiseSeasonId, 2024);

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(franchiseSeasonId);

        var savedMetric = await FootballDataContext.FranchiseSeasonMetrics
            .FirstOrDefaultAsync(x => x.FranchiseSeasonId == franchiseSeasonId);

        savedMetric.Should().BeNull();
    }

    [Fact]
    public async Task WhenExistingMetricExists_ShouldReplaceIt()
    {
        // Arrange
        var sut = Mocker.CreateInstance<CalculateFranchiseSeasonMetricsCommandHandler>();

        var franchiseSeasonId = Guid.NewGuid();
        var seasonYear = 2024;

        // Create existing metric
        var existingMetric = Fixture.Build<FranchiseSeasonMetric>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.FranchiseSeasonId, franchiseSeasonId)
            .With(x => x.Season, seasonYear)
            .With(x => x.GamesPlayed, 5)
            .With(x => x.Ypp, 4.0m)
            .Create();

        await FootballDataContext.FranchiseSeasonMetrics.AddAsync(existingMetric);

        // Create contest and competition
        var contest = CreateContest(franchiseSeasonId, seasonYear);
        await FootballDataContext.Contests.AddAsync(contest);

        var competition = Fixture.Build<Competition>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.ContestId, contest.Id)
            .With(x => x.Contest, contest)
            .Create();

        await FootballDataContext.Competitions.AddAsync(competition);

        var competitionMetric = Fixture.Build<CompetitionMetric>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.CompetitionId, competition.Id)
            .With(x => x.FranchiseSeasonId, franchiseSeasonId)
            .With(x => x.Season, seasonYear)
            .With(x => x.Ypp, 6.0m)
            .With(x => x.SuccessRate, 0.50m)
            .With(x => x.ExplosiveRate, 0.15m)
            .With(x => x.PointsPerDrive, 2.5m)
            .With(x => x.ThirdFourthRate, 0.45m)
            .With(x => x.TimePossRatio, 0.55m)
            .With(x => x.OppYpp, 4.5m)
            .With(x => x.OppSuccessRate, 0.35m)
            .With(x => x.OppExplosiveRate, 0.07m)
            .With(x => x.OppPointsPerDrive, 1.5m)
            .With(x => x.OppThirdFourthRate, 0.30m)
            .With(x => x.NetPunt, 40.0m)
            .With(x => x.FgPctShrunk, 0.90m)
            .With(x => x.FieldPosDiff, 5.0m)
            .With(x => x.TurnoverMarginPerDrive, 0.20m)
            .With(x => x.PenaltyYardsPerPlay, 0.4m)
            .Create();

        await FootballDataContext.CompetitionMetrics.AddAsync(competitionMetric);
        await FootballDataContext.SaveChangesAsync();

        var command = new CalculateFranchiseSeasonMetricsCommand(franchiseSeasonId, seasonYear);

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var metrics = await FootballDataContext.FranchiseSeasonMetrics
            .Where(x => x.FranchiseSeasonId == franchiseSeasonId)
            .ToListAsync();

        metrics.Should().HaveCount(1);
        metrics[0].GamesPlayed.Should().Be(1);
        metrics[0].Ypp.Should().Be(6.0m);
    }

    private Contest CreateContest(Guid franchiseSeasonId, int seasonYear)
    {
        return Fixture.Build<Contest>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Name, "Test Game")
            .With(x => x.ShortName, "Test")
            .With(x => x.HomeTeamFranchiseSeasonId, franchiseSeasonId)
            .With(x => x.SeasonYear, seasonYear)
            .Create();
    }

    private CompetitionMetric CreateCompetitionMetric(Guid competitionId, Guid franchiseSeasonId, int seasonYear)
    {
        return Fixture.Build<CompetitionMetric>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.CompetitionId, competitionId)
            .With(x => x.FranchiseSeasonId, franchiseSeasonId)
            .With(x => x.Season, seasonYear)
            .With(x => x.Ypp, 5.5m)
            .With(x => x.SuccessRate, 0.45m)
            .With(x => x.ExplosiveRate, 0.12m)
            .With(x => x.PointsPerDrive, 2.1m)
            .With(x => x.ThirdFourthRate, 0.40m)
            .With(x => x.TimePossRatio, 0.52m)
            .With(x => x.OppYpp, 4.8m)
            .With(x => x.OppSuccessRate, 0.38m)
            .With(x => x.OppExplosiveRate, 0.08m)
            .With(x => x.OppPointsPerDrive, 1.8m)
            .With(x => x.OppThirdFourthRate, 0.35m)
            .With(x => x.NetPunt, 38.5m)
            .With(x => x.FgPctShrunk, 0.85m)
            .With(x => x.FieldPosDiff, 3.2m)
            .With(x => x.TurnoverMarginPerDrive, 0.15m)
            .With(x => x.PenaltyYardsPerPlay, 0.5m)
            .Create();
    }
}
