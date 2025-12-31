using FluentAssertions;

using Moq;

using SportsData.Api.Application;
using SportsData.Api.Application.Previews;
using SportsData.Api.Application.UI.Leagues.Commands.GenerateLeagueWeekPreviews;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Processing;

using Xunit;

using League = SportsData.Api.Application.League;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Commands.GenerateLeagueWeekPreviews;

public class GenerateLeagueWeekPreviewsCommandHandlerTests : ApiTestBase<GenerateLeagueWeekPreviewsCommandHandler>
{
    private readonly Mock<IProvideBackgroundJobs> _backgroundJobProviderMock;

    public GenerateLeagueWeekPreviewsCommandHandlerTests()
    {
        _backgroundJobProviderMock = Mocker.GetMock<IProvideBackgroundJobs>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenLeagueNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GenerateLeagueWeekPreviewsCommandHandler>();
        var command = new GenerateLeagueWeekPreviewsCommand
        {
            LeagueId = Guid.NewGuid(),
            WeekNumber = 1
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        var failure = result as Failure<Guid>;
        failure!.Errors.Should().ContainSingle(e => e.PropertyName == nameof(command.LeagueId));
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoMatchupsInWeek_ReturnsSuccessWithNoJobsEnqueued()
    {
        // Arrange
        var league = CreateLeague();
        DataContext.PickemGroups.Add(league);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GenerateLeagueWeekPreviewsCommandHandler>();
        var command = new GenerateLeagueWeekPreviewsCommand
        {
            LeagueId = league.Id,
            WeekNumber = 1
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(league.Id);
        _backgroundJobProviderMock.Verify(
            x => x.Enqueue(It.IsAny<System.Linq.Expressions.Expression<Func<MatchupPreviewProcessor, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMatchupsExistWithExistingPreviews_SkipsThoseMatchups()
    {
        // Arrange
        var league = CreateLeague();
        DataContext.PickemGroups.Add(league);

        var contestId1 = Guid.NewGuid();
        var contestId2 = Guid.NewGuid();

        var matchup1 = CreateMatchup(league.Id, contestId1, weekNumber: 5);
        var matchup2 = CreateMatchup(league.Id, contestId2, weekNumber: 5);
        DataContext.PickemGroupMatchups.AddRange(matchup1, matchup2);

        // Create existing preview for contestId1 (not rejected)
        var existingPreview = CreateMatchupPreview(contestId1, rejected: false);
        DataContext.MatchupPreviews.Add(existingPreview);

        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GenerateLeagueWeekPreviewsCommandHandler>();
        var command = new GenerateLeagueWeekPreviewsCommand
        {
            LeagueId = league.Id,
            WeekNumber = 5
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Should only enqueue for contestId2 since contestId1 has an existing preview
        _backgroundJobProviderMock.Verify(
            x => x.Enqueue(It.IsAny<System.Linq.Expressions.Expression<Func<MatchupPreviewProcessor, Task>>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMatchupsExistWithRejectedPreviews_EnqueuesJobsForThose()
    {
        // Arrange
        var league = CreateLeague();
        DataContext.PickemGroups.Add(league);

        var contestId = Guid.NewGuid();
        var matchup = CreateMatchup(league.Id, contestId, weekNumber: 5);
        DataContext.PickemGroupMatchups.Add(matchup);

        // Create rejected preview - should be treated as not existing
        var rejectedPreview = CreateMatchupPreview(contestId, rejected: true);
        DataContext.MatchupPreviews.Add(rejectedPreview);

        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GenerateLeagueWeekPreviewsCommandHandler>();
        var command = new GenerateLeagueWeekPreviewsCommand
        {
            LeagueId = league.Id,
            WeekNumber = 5
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Should enqueue since the existing preview was rejected
        _backgroundJobProviderMock.Verify(
            x => x.Enqueue(It.IsAny<System.Linq.Expressions.Expression<Func<MatchupPreviewProcessor, Task>>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMatchupsExistWithNoPreviews_EnqueuesJobsForAll()
    {
        // Arrange
        var league = CreateLeague();
        DataContext.PickemGroups.Add(league);

        var contestId1 = Guid.NewGuid();
        var contestId2 = Guid.NewGuid();
        var contestId3 = Guid.NewGuid();

        var matchup1 = CreateMatchup(league.Id, contestId1, weekNumber: 10);
        var matchup2 = CreateMatchup(league.Id, contestId2, weekNumber: 10);
        var matchup3 = CreateMatchup(league.Id, contestId3, weekNumber: 10);
        DataContext.PickemGroupMatchups.AddRange(matchup1, matchup2, matchup3);

        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GenerateLeagueWeekPreviewsCommandHandler>();
        var command = new GenerateLeagueWeekPreviewsCommand
        {
            LeagueId = league.Id,
            WeekNumber = 10
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(league.Id);
        _backgroundJobProviderMock.Verify(
            x => x.Enqueue(It.IsAny<System.Linq.Expressions.Expression<Func<MatchupPreviewProcessor, Task>>>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task ExecuteAsync_OnlyProcessesMatchupsForSpecifiedWeek()
    {
        // Arrange
        var league = CreateLeague();
        DataContext.PickemGroups.Add(league);

        var contestWeek5 = Guid.NewGuid();
        var contestWeek6 = Guid.NewGuid();

        var matchupWeek5 = CreateMatchup(league.Id, contestWeek5, weekNumber: 5);
        var matchupWeek6 = CreateMatchup(league.Id, contestWeek6, weekNumber: 6);
        DataContext.PickemGroupMatchups.AddRange(matchupWeek5, matchupWeek6);

        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GenerateLeagueWeekPreviewsCommandHandler>();
        var command = new GenerateLeagueWeekPreviewsCommand
        {
            LeagueId = league.Id,
            WeekNumber = 5 // Only week 5
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Should only enqueue for week 5 matchup
        _backgroundJobProviderMock.Verify(
            x => x.Enqueue(It.IsAny<System.Linq.Expressions.Expression<Func<MatchupPreviewProcessor, Task>>>()),
            Times.Once);
    }

    #region Helper Methods

    private static PickemGroup CreateLeague(string name = "Test League")
    {
        var commissionerId = Guid.NewGuid();
        return new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = name,
            CommissionerUserId = commissionerId,
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            IsPublic = false,
            UseConfidencePoints = false,
            CreatedBy = commissionerId,
            CreatedUtc = DateTime.UtcNow
        };
    }

    private static PickemGroupMatchup CreateMatchup(Guid groupId, Guid contestId, int weekNumber)
    {
        return new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            SeasonWeekId = Guid.NewGuid(),
            SeasonYear = 2025,
            SeasonWeek = weekNumber,
            StartDateUtc = DateTime.UtcNow.AddDays(1),
            CreatedBy = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow
        };
    }

    private static MatchupPreview CreateMatchupPreview(Guid contestId, bool rejected)
    {
        return new MatchupPreview
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            Overview = "Test overview",
            Analysis = "Test analysis",
            Prediction = "Test prediction",
            RejectedUtc = rejected ? DateTime.UtcNow : null,
            CreatedUtc = DateTime.UtcNow
        };
    }

    #endregion
}
