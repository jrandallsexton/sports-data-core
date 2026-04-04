using AutoFixture;

using Moq;

using SportsData.Api.Application.Jobs;
using SportsData.Api.Application.Scoring;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Processing;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Scoring;

public class ContestScoringJobTests : ApiTestBase<ContestScoringJob>
{
    private readonly Mock<IProvideSeasons> _seasonClientMock = new();
    private readonly Mock<IProvideContests> _contestClientMock = new();

    public ContestScoringJobTests()
    {
        Mocker.GetMock<ISeasonClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_seasonClientMock.Object);
        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_contestClientMock.Object);
    }

    [Fact]
    public async Task Process_Should_Enqueue_ScoreContestCommand_For_Each_Finalized_Unscored_Contest()
    {
        // Arrange
        var seasonWeekId = Guid.NewGuid();

        var currentWeek = Fixture.Build<CanonicalSeasonWeekDto>()
            .With(x => x.Id, seasonWeekId)
            .Create();

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        _seasonClientMock
            .Setup(x => x.GetCurrentAndLastSeasonWeeks(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<CanonicalSeasonWeekDto>>([currentWeek]));

        // These are the contest IDs referenced by unscored picks
        var contestId1 = Guid.NewGuid();
        var contestId2 = Guid.NewGuid();
        var contestId3 = Guid.NewGuid(); // This one is NOT finalized

        DataContext.UserPicks.AddRange(
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, contestId1)
                .With(x => x.ScoredAt, (DateTime?)null).Create(),
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, contestId2)
                .With(x => x.ScoredAt, (DateTime?)null).Create(),
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, contestId3)
                .With(x => x.ScoredAt, (DateTime?)null).Create()
        );

        await DataContext.SaveChangesAsync();

        // Contest client returns 2 of the 3 as finalized
        _contestClientMock
            .Setup(x => x.GetFinalizedContestIds(seasonWeekId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<Guid>>([contestId1, contestId2]));

        var sut = Mocker.CreateInstance<ContestScoringJob>();

        // Act
        await sut.ExecuteAsync();

        // Assert
        background.Verify(x => x.Enqueue<IScoreContests>(
            It.IsAny<Expression<Func<IScoreContests, Task>>>()), Times.Exactly(2));
    }
}
