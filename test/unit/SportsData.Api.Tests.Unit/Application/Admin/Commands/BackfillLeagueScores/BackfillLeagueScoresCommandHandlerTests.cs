using FluentAssertions;
using FluentValidation;

using Moq;

using SportsData.Api.Application.Admin.Commands.BackfillLeagueScores;
using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.Commands.BackfillLeagueScores;

public class BackfillLeagueScoresCommandHandlerTests : ApiTestBase<BackfillLeagueScoresCommandHandler>
{
    public BackfillLeagueScoresCommandHandlerTests()
    {
        // Register validator
        Mocker.Use<IValidator<BackfillLeagueScoresCommand>>(new BackfillLeagueScoresCommandValidator());
    }
    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenSeasonYearIsTooOld()
    {
        // Arrange
        var handler = Mocker.CreateInstance<BackfillLeagueScoresCommandHandler>();
        var command = new BackfillLeagueScoresCommand(1999);

        // Act
        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        var failure = (Failure<BackfillLeagueScoresResult>)result;
        failure.Errors.Should().NotBeEmpty();
        failure.Errors[0].ErrorMessage.Should().Contain("greater than 2000");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenSeasonYearIsTooFarInFuture()
    {
        // Arrange
        var handler = Mocker.CreateInstance<BackfillLeagueScoresCommandHandler>();
        var futureYear = DateTime.UtcNow.Year + 5;
        var command = new BackfillLeagueScoresCommand(futureYear);

        // Act
        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        var failure = (Failure<BackfillLeagueScoresResult>)result;
        failure.Errors.Should().NotBeEmpty();
        failure.Errors[0].ErrorMessage.Should().Contain("future");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoWeeksFound_WhenNoCompletedWeeksExist()
    {
        // Arrange
        var seasonYear = 2024;
        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetCompletedSeasonWeeks(seasonYear))
            .ReturnsAsync(new List<SeasonWeek>());

        var handler = Mocker.CreateInstance<BackfillLeagueScoresCommandHandler>();
        var command = new BackfillLeagueScoresCommand(seasonYear);

        // Act
        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Value.SeasonYear.Should().Be(seasonYear);
        result.Value.TotalWeeks.Should().Be(0);
        result.Value.ProcessedWeeks.Should().Be(0);
        result.Value.Errors.Should().Be(0);
        result.Value.Message.Should().Contain("No completed weeks found");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProcessAllWeeks_WhenWeeksExist()
    {
        // Arrange
        var seasonYear = 2024;
        var seasonWeeks = new List<SeasonWeek>
        {
            new() { SeasonYear = seasonYear, WeekNumber = 1 },
            new() { SeasonYear = seasonYear, WeekNumber = 2 }
        };

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetCompletedSeasonWeeks(seasonYear))
            .ReturnsAsync(seasonWeeks);

        // Create test data in database
        var group1 = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Test League 1",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            CommissionerUserId = Guid.NewGuid()
        };

        var group2 = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Test League 2",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            CommissionerUserId = Guid.NewGuid()
        };

        await DataContext.PickemGroups.AddRangeAsync(group1, group2);

        // Add matchups for both weeks and both leagues
        var matchups = new List<PickemGroupMatchup>
        {
            new() { Id = Guid.NewGuid(), GroupId = group1.Id, SeasonYear = seasonYear, SeasonWeek = 1, ContestId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), GroupId = group1.Id, SeasonYear = seasonYear, SeasonWeek = 2, ContestId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), GroupId = group2.Id, SeasonYear = seasonYear, SeasonWeek = 1, ContestId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), GroupId = group2.Id, SeasonYear = seasonYear, SeasonWeek = 2, ContestId = Guid.NewGuid() }
        };

        await DataContext.PickemGroupMatchups.AddRangeAsync(matchups);
        await DataContext.SaveChangesAsync();

        // Mock the scoring service
        Mocker.GetMock<ILeagueWeekScoringService>()
            .Setup(x => x.ScoreLeagueWeekAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = Mocker.CreateInstance<BackfillLeagueScoresCommandHandler>();
        var command = new BackfillLeagueScoresCommand(seasonYear);

        // Act
        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Value.SeasonYear.Should().Be(seasonYear);
        result.Value.TotalWeeks.Should().Be(2);
        result.Value.ProcessedWeeks.Should().Be(4); // 2 leagues × 2 weeks
        result.Value.Errors.Should().Be(0);
        result.Value.Message.Should().Contain("Backfill completed");

        // Verify scoring service was called 4 times
        Mocker.GetMock<ILeagueWeekScoringService>()
            .Verify(x => x.ScoreLeagueWeekAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldContinueProcessing_WhenOneLeagueWeekFails()
    {
        // Arrange
        var seasonYear = 2024;
        var seasonWeeks = new List<SeasonWeek>
        {
            new() { SeasonYear = seasonYear, WeekNumber = 1 }
        };

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetCompletedSeasonWeeks(seasonYear))
            .ReturnsAsync(seasonWeeks);

        var group1 = new PickemGroup { Id = Guid.NewGuid(), Name = "League 1", Sport = Sport.FootballNcaa, League = League.NCAAF, CommissionerUserId = Guid.NewGuid() };
        var group2 = new PickemGroup { Id = Guid.NewGuid(), Name = "League 2", Sport = Sport.FootballNcaa, League = League.NCAAF, CommissionerUserId = Guid.NewGuid() };

        await DataContext.PickemGroups.AddRangeAsync(group1, group2);
        await DataContext.PickemGroupMatchups.AddRangeAsync(
            new PickemGroupMatchup { Id = Guid.NewGuid(), GroupId = group1.Id, SeasonYear = seasonYear, SeasonWeek = 1, ContestId = Guid.NewGuid() },
            new PickemGroupMatchup { Id = Guid.NewGuid(), GroupId = group2.Id, SeasonYear = seasonYear, SeasonWeek = 1, ContestId = Guid.NewGuid() }
        );
        await DataContext.SaveChangesAsync();

        // First call fails, second succeeds
        var callCount = 0;
        Mocker.GetMock<ILeagueWeekScoringService>()
            .Setup(x => x.ScoreLeagueWeekAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, int, int, CancellationToken>((leagueId, year, week, ct) =>
            {
                callCount++;
                if (callCount == 1)
                    throw new Exception("Scoring failed");
                return Task.CompletedTask;
            });

        var handler = Mocker.CreateInstance<BackfillLeagueScoresCommandHandler>();
        var command = new BackfillLeagueScoresCommand(seasonYear);

        // Act
        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Value.SeasonYear.Should().Be(seasonYear);
        result.Value.TotalWeeks.Should().Be(1);
        result.Value.ProcessedWeeks.Should().Be(1); // One succeeded
        result.Value.Errors.Should().Be(1); // One failed
        result.Value.Message.Should().Contain("1 errors");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleException_WhenCanonicalDataThrows()
    {
        // Arrange
        var seasonYear = 2024;

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetCompletedSeasonWeeks(seasonYear))
            .ThrowsAsync(new Exception("Database error"));

        var handler = Mocker.CreateInstance<BackfillLeagueScoresCommandHandler>();
        var command = new BackfillLeagueScoresCommand(seasonYear);

        // Act
        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        var failure = (Failure<BackfillLeagueScoresResult>)result;
        failure.Errors.Should().NotBeEmpty();
        failure.Errors[0].ErrorMessage.Should().Be("An error occurred while backfilling league scores");
        failure.Value.Should().NotBeNull();
        failure.Value.Should().Be(BackfillLeagueScoresResult.Empty());
    }
}
