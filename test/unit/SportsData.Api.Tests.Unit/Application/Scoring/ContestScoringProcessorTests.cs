using AutoFixture;
using SportsData.Api.Application.Common.Enums;

using Moq;

using SportsData.Api.Application;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Api.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Scoring;

public class ContestScoringProcessorTests : ApiTestBase<ContestScoringProcessor>
{
    [Fact]
    public async Task Process_WithValidMatchupResult_ScoresEachPick()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var seasonWeekId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var result = Fixture.Build<MatchupResult>()
            .With(x => x.WinnerFranchiseSeasonId, Guid.NewGuid())
            .With(x => x.ContestId, contestId)
            .With(x => x.SeasonWeekId, seasonWeekId)
            .Create();

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetMatchupResult(contestId))
            .ReturnsAsync(result);

        var matchup = Fixture.Build<PickemGroupMatchup>()
            .With(x => x.ContestId, contestId)
            .With(x => x.SeasonWeekId, seasonWeekId)
            .Create();

        var group = Fixture.Build<PickemGroup>()
            .With(x => x.Id, groupId)
            .With(x => x.PickType, PickType.StraightUp)
            .With (x => x.Weeks, new List<PickemGroupWeek>
            {
                new()
                {
                    SeasonWeekId = seasonWeekId,
                    Matchups = new List<PickemGroupMatchup>
                    {
                        matchup
                    },
                    SeasonYear = 2025,
                    SeasonWeek = 2,
                    GroupId = groupId
                }
            })
            .Create();

        await DataContext.PickemGroups.AddAsync(group);

        var picks = Fixture.Build<PickemGroupUserPick>()
            .With(x => x.ContestId, contestId)
            .With(x => x.PickemGroupId, groupId)
            .With(x => x.Group, group)
            .With(x => x.FranchiseId, result.WinnerFranchiseSeasonId) // make it correct
            .CreateMany(3)
            .ToList();

        await DataContext.UserPicks.AddRangeAsync(picks);
        await DataContext.SaveChangesAsync();

        var command = new ScoreContestCommand(contestId);

        var sut = Mocker.CreateInstance<ContestScoringProcessor>();

        // Act
        await sut.Process(command);

        // Assert
        var scoring = Mocker.GetMock<IPickScoringService>();

        foreach (var pick in picks)
        {
            scoring.Verify(s =>
                s.ScorePick(
                    It.Is<PickemGroup>(g => g.Id == groupId),
                    It.IsAny<double?>(),
                    It.Is<PickemGroupUserPick>(p => p.Id == pick.Id),
                    result),
                Times.Once);
        }

        // Also ensure the result was fetched
        Mocker.GetMock<IProvideCanonicalData>()
            .Verify(x => x.GetMatchupResult(contestId), Times.Once);
    }

    [Fact]
    public async Task Process_WhenResultNotFound_LogsAndReturns()
    {
        // Arrange
        var contestId = Guid.NewGuid();

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetMatchupResult(contestId))!
            .ReturnsAsync((MatchupResult?)null);

        var command = new ScoreContestCommand(contestId);

        var sut = Mocker.CreateInstance<ContestScoringProcessor>();

        // Act
        await sut.Process(command);

        // Assert
        Mocker.GetMock<IPickScoringService>()
            .Verify(x => x.ScorePick(
                    It.IsAny<PickemGroup>(),
                    It.IsAny<double?>(),
                    It.IsAny<PickemGroupUserPick>(),
                    It.IsAny<MatchupResult>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WhenGroupNotFound_LogsAndSkips()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var missingGroupId = Guid.NewGuid();

        var result = Fixture.Build<MatchupResult>()
            .With(x => x.WinnerFranchiseSeasonId, Guid.NewGuid())
            .Create();

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetMatchupResult(contestId))
            .ReturnsAsync(result);

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(x => x.ContestId, contestId)
            .With(x => x.PickemGroupId, missingGroupId)
            .With(x => x.Group, null as PickemGroup)
            .Create();

        await DataContext.UserPicks.AddAsync(pick);
        await DataContext.SaveChangesAsync();

        var command = new ScoreContestCommand(contestId);

        var sut = Mocker.CreateInstance<ContestScoringProcessor>();

        // Act
        await sut.Process(command);

        // Assert
        Mocker.GetMock<IPickScoringService>()
            .Verify(x => x.ScorePick(
                    It.IsAny<PickemGroup>(),
                    It.IsAny<double?>(),
                    It.IsAny<PickemGroupUserPick>(),
                    It.IsAny<MatchupResult>()),
            Times.Never);
    }
}
