using FluentAssertions;
using FluentValidation;

using Moq;

using SportsData.Api.Application.Admin.Commands.BackfillLeagueScores;
using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Scoring;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Infrastructure.Clients.Season;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.Commands.BackfillLeagueScores;

public class BackfillLeagueScoresCommandHandlerTests : ApiTestBase<BackfillLeagueScoresCommandHandler>
{
    private readonly Mock<IProvideSeasons> _seasonClientMock = new();

    public BackfillLeagueScoresCommandHandlerTests()
    {
        Mocker.GetMock<ISeasonClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_seasonClientMock.Object);
        Mocker.Use<IValidator<BackfillLeagueScoresCommand>>(new BackfillLeagueScoresCommandValidator());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenSeasonYearIsTooOld()
    {
        var handler = Mocker.CreateInstance<BackfillLeagueScoresCommandHandler>();
        var command = new BackfillLeagueScoresCommand(1999);

        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        var failure = (Failure<BackfillLeagueScoresResult>)result;
        failure.Errors.Should().NotBeEmpty();
        failure.Errors[0].ErrorMessage.Should().Contain("greater than 2000");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenSeasonYearIsTooFarInFuture()
    {
        var handler = Mocker.CreateInstance<BackfillLeagueScoresCommandHandler>();
        var futureYear = DateTime.UtcNow.Year + 5;
        var command = new BackfillLeagueScoresCommand(futureYear);

        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        var failure = (Failure<BackfillLeagueScoresResult>)result;
        failure.Errors.Should().NotBeEmpty();
        failure.Errors[0].ErrorMessage.Should().Contain("future");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoWeeksFound_WhenNoCompletedWeeksExist()
    {
        var seasonYear = 2024;
        _seasonClientMock
            .Setup(x => x.GetCompletedSeasonWeeks(seasonYear, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<CanonicalSeasonWeekDto>>(new List<CanonicalSeasonWeekDto>()));

        var handler = Mocker.CreateInstance<BackfillLeagueScoresCommandHandler>();
        var command = new BackfillLeagueScoresCommand(seasonYear);

        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        result.Value.SeasonYear.Should().Be(seasonYear);
        result.Value.TotalWeeks.Should().Be(0);
        result.Value.ProcessedWeeks.Should().Be(0);
        result.Value.Errors.Should().Be(0);
        result.Value.Message.Should().Contain("No completed weeks found");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProcessAllWeeks_WhenWeeksExist()
    {
        var seasonYear = 2024;
        var seasonWeeks = new List<CanonicalSeasonWeekDto>
        {
            new() { SeasonYear = seasonYear, WeekNumber = 1 },
            new() { SeasonYear = seasonYear, WeekNumber = 2 }
        };

        _seasonClientMock
            .Setup(x => x.GetCompletedSeasonWeeks(seasonYear, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<CanonicalSeasonWeekDto>>(seasonWeeks));

        var group1 = new PickemGroup
        {
            Id = Guid.NewGuid(), Name = "Test League 1",
            Sport = Sport.FootballNcaa, League = League.NCAAF,
            CommissionerUserId = Guid.NewGuid()
        };
        var group2 = new PickemGroup
        {
            Id = Guid.NewGuid(), Name = "Test League 2",
            Sport = Sport.FootballNcaa, League = League.NCAAF,
            CommissionerUserId = Guid.NewGuid()
        };

        await DataContext.PickemGroups.AddRangeAsync(group1, group2);
        await DataContext.PickemGroupMatchups.AddRangeAsync(
            new PickemGroupMatchup { Id = Guid.NewGuid(), GroupId = group1.Id, SeasonYear = seasonYear, SeasonWeek = 1, ContestId = Guid.NewGuid() },
            new PickemGroupMatchup { Id = Guid.NewGuid(), GroupId = group1.Id, SeasonYear = seasonYear, SeasonWeek = 2, ContestId = Guid.NewGuid() },
            new PickemGroupMatchup { Id = Guid.NewGuid(), GroupId = group2.Id, SeasonYear = seasonYear, SeasonWeek = 1, ContestId = Guid.NewGuid() },
            new PickemGroupMatchup { Id = Guid.NewGuid(), GroupId = group2.Id, SeasonYear = seasonYear, SeasonWeek = 2, ContestId = Guid.NewGuid() }
        );
        await DataContext.SaveChangesAsync();

        Mocker.GetMock<ILeagueWeekScoringService>()
            .Setup(x => x.ScoreLeagueWeekAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = Mocker.CreateInstance<BackfillLeagueScoresCommandHandler>();
        var command = new BackfillLeagueScoresCommand(seasonYear);

        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        result.Value.SeasonYear.Should().Be(seasonYear);
        result.Value.TotalWeeks.Should().Be(2);
        result.Value.ProcessedWeeks.Should().Be(4);
        result.Value.Errors.Should().Be(0);
        result.Value.Message.Should().Contain("Backfill completed");

        Mocker.GetMock<ILeagueWeekScoringService>()
            .Verify(x => x.ScoreLeagueWeekAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldContinueProcessing_WhenOneLeagueWeekFails()
    {
        var seasonYear = 2024;
        var seasonWeeks = new List<CanonicalSeasonWeekDto>
        {
            new() { SeasonYear = seasonYear, WeekNumber = 1 }
        };

        _seasonClientMock
            .Setup(x => x.GetCompletedSeasonWeeks(seasonYear, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<CanonicalSeasonWeekDto>>(seasonWeeks));

        var group1 = new PickemGroup { Id = Guid.NewGuid(), Name = "League 1", Sport = Sport.FootballNcaa, League = League.NCAAF, CommissionerUserId = Guid.NewGuid() };
        var group2 = new PickemGroup { Id = Guid.NewGuid(), Name = "League 2", Sport = Sport.FootballNcaa, League = League.NCAAF, CommissionerUserId = Guid.NewGuid() };

        await DataContext.PickemGroups.AddRangeAsync(group1, group2);
        await DataContext.PickemGroupMatchups.AddRangeAsync(
            new PickemGroupMatchup { Id = Guid.NewGuid(), GroupId = group1.Id, SeasonYear = seasonYear, SeasonWeek = 1, ContestId = Guid.NewGuid() },
            new PickemGroupMatchup { Id = Guid.NewGuid(), GroupId = group2.Id, SeasonYear = seasonYear, SeasonWeek = 1, ContestId = Guid.NewGuid() }
        );
        await DataContext.SaveChangesAsync();

        var callCount = 0;
        Mocker.GetMock<ILeagueWeekScoringService>()
            .Setup(x => x.ScoreLeagueWeekAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, int, int, CancellationToken>((leagueId, year, week, ct) =>
            {
                callCount++;
                if (callCount == 1) throw new Exception("Scoring failed");
                return Task.CompletedTask;
            });

        var handler = Mocker.CreateInstance<BackfillLeagueScoresCommandHandler>();
        var command = new BackfillLeagueScoresCommand(seasonYear);

        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        result.Value.SeasonYear.Should().Be(seasonYear);
        result.Value.TotalWeeks.Should().Be(1);
        result.Value.ProcessedWeeks.Should().Be(1);
        result.Value.Errors.Should().Be(1);
        result.Value.Message.Should().Contain("1 errors");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleException_WhenSeasonClientThrows()
    {
        var seasonYear = 2024;

        _seasonClientMock
            .Setup(x => x.GetCompletedSeasonWeeks(seasonYear, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection error"));

        var handler = Mocker.CreateInstance<BackfillLeagueScoresCommandHandler>();
        var command = new BackfillLeagueScoresCommand(seasonYear);

        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        var failure = (Failure<BackfillLeagueScoresResult>)result;
        failure.Errors.Should().NotBeEmpty();
        failure.Errors[0].ErrorMessage.Should().Be("An error occurred while backfilling league scores");
    }
}
