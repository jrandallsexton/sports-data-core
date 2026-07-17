using FluentAssertions;
using SportsData.Api.Application.Common.Enums;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application;
using SportsData.Api.Application.UI.Picks.Commands.SubmitPick;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Moq;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Picks.Commands.SubmitPick;

public class SubmitPickCommandHandlerTests : ApiTestBase<SubmitPickCommandHandler>
{
    // Fixed "now" so matchup lock state is deterministic — the handler now checks
    // lock via the injected IDateTimeProvider (configured below) rather than the
    // real wall clock.
    private static readonly DateTime Now = new(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

    public SubmitPickCommandHandlerTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenGroupDoesNotExist()
    {
        // Arrange
        var handler = Mocker.CreateInstance<SubmitPickCommandHandler>();

        var command = new SubmitPickCommand
        {
            UserId = Guid.NewGuid(),
            PickemGroupId = Guid.NewGuid(),
            ContestId = Guid.NewGuid(),
            PickType = PickType.StraightUp
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenMatchupDoesNotExist()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<SubmitPickCommandHandler>();

        var command = new SubmitPickCommand
        {
            UserId = Guid.NewGuid(),
            PickemGroupId = groupId,
            ContestId = Guid.NewGuid(),
            PickType = PickType.StraightUp
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidation_WhenMatchupIsLocked()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };
        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            StartDateUtc = Now.AddMinutes(2) // Within lock window (5 min)
        };

        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<SubmitPickCommandHandler>();

        var command = new SubmitPickCommand
        {
            UserId = Guid.NewGuid(),
            PickemGroupId = groupId,
            ContestId = contestId,
            PickType = PickType.StraightUp
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidation_WhenOverUnderTypeWithNoSelection()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };
        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            StartDateUtc = Now.AddHours(1)
        };

        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<SubmitPickCommandHandler>();

        var command = new SubmitPickCommand
        {
            UserId = Guid.NewGuid(),
            PickemGroupId = groupId,
            ContestId = contestId,
            PickType = PickType.OverUnder,
            OverUnder = OverUnderPick.None
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateNewPick_WhenValid()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var franchiseId = Guid.NewGuid();

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };
        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            StartDateUtc = Now.AddHours(1)
        };

        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<SubmitPickCommandHandler>();

        var command = new SubmitPickCommand
        {
            UserId = userId,
            PickemGroupId = groupId,
            ContestId = contestId,
            Week = 5,
            PickType = PickType.StraightUp,
            FranchiseSeasonId = franchiseId,
            ConfidencePoints = 7,
            TiebreakerGuessTotal = 52,
            TiebreakerGuessHome = 28,
            TiebreakerGuessAway = 24
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(contestId);

        var pick = await DataContext.UserPicks.FirstOrDefaultAsync();
        pick.Should().NotBeNull();
        pick!.UserId.Should().Be(command.UserId);
        pick.PickemGroupId.Should().Be(command.PickemGroupId);
        pick.ContestId.Should().Be(command.ContestId);
        pick.PickType.Should().Be(command.PickType);
        pick.FranchiseSeasonId.Should().Be(command.FranchiseSeasonId);
        pick.ConfidencePoints.Should().Be(command.ConfidencePoints);
        pick.TiebreakerGuessTotal.Should().Be(command.TiebreakerGuessTotal);
        pick.TiebreakerGuessHome.Should().Be(command.TiebreakerGuessHome);
        pick.TiebreakerGuessAway.Should().Be(command.TiebreakerGuessAway);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUpdateExistingPick_WhenOneExists()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };
        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            StartDateUtc = Now.AddHours(1)
        };
        var existingPick = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = groupId,
            ContestId = contestId,
            PickType = PickType.StraightUp,
            FranchiseSeasonId = Guid.NewGuid(),
            TiebreakerType = TiebreakerType.TotalPoints
        };

        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.UserPicks.AddAsync(existingPick);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<SubmitPickCommandHandler>();
        var newFranchiseId = Guid.NewGuid();

        var command = new SubmitPickCommand
        {
            UserId = userId,
            PickemGroupId = groupId,
            ContestId = contestId,
            PickType = PickType.StraightUp,
            FranchiseSeasonId = newFranchiseId,
            OverUnder = OverUnderPick.Under,
            ConfidencePoints = 3,
            TiebreakerGuessTotal = 45,
            TiebreakerGuessHome = 21,
            TiebreakerGuessAway = 24
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updated = await DataContext.UserPicks.FirstOrDefaultAsync();
        updated.Should().NotBeNull();
        updated!.FranchiseSeasonId.Should().Be(newFranchiseId);
        updated.OverUnder.Should().Be(OverUnderPick.Under);
        updated.ConfidencePoints.Should().Be(3);
        updated.TiebreakerGuessTotal.Should().Be(45);
        updated.TiebreakerGuessHome.Should().Be(21);
        updated.TiebreakerGuessAway.Should().Be(24);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPersistImportedFromPickId_WhenProvided()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var sourcePickId = Guid.NewGuid();

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };
        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            StartDateUtc = Now.AddHours(1)
        };

        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<SubmitPickCommandHandler>();

        var command = new SubmitPickCommand
        {
            UserId = Guid.NewGuid(),
            PickemGroupId = groupId,
            ContestId = contestId,
            Week = 5,
            PickType = PickType.StraightUp,
            FranchiseSeasonId = Guid.NewGuid(),
            ImportedFromPickId = sourcePickId
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var pick = await DataContext.UserPicks.FirstOrDefaultAsync();
        pick!.ImportedFromPickId.Should().Be(sourcePickId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidation_WhenConfidenceLeagueAndConfidenceMissing()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Confidence League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            UseConfidencePoints = true
        };
        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            StartDateUtc = Now.AddHours(1)
        };
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<SubmitPickCommandHandler>();

        var command = new SubmitPickCommand
        {
            UserId = Guid.NewGuid(),
            PickemGroupId = groupId,
            ContestId = contestId,
            Week = 5,
            PickType = PickType.StraightUp,
            FranchiseSeasonId = Guid.NewGuid(),
            ConfidencePoints = null // unranked — must be rejected in a confidence league
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        (await DataContext.UserPicks.FirstOrDefaultAsync()).Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSucceed_WhenConfidenceLeagueAndConfidenceProvided()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        var group = new PickemGroup
        {
            Id = groupId,
            Name = "Confidence League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            UseConfidencePoints = true
        };
        var matchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            StartDateUtc = Now.AddHours(1)
        };
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<SubmitPickCommandHandler>();

        var command = new SubmitPickCommand
        {
            UserId = Guid.NewGuid(),
            PickemGroupId = groupId,
            ContestId = contestId,
            Week = 5,
            PickType = PickType.StraightUp,
            FranchiseSeasonId = Guid.NewGuid(),
            ConfidencePoints = 5
        };

        // Act
        var result = await handler.ExecuteAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var pick = await DataContext.UserPicks.FirstOrDefaultAsync();
        pick!.ConfidencePoints.Should().Be(5);
    }
}
