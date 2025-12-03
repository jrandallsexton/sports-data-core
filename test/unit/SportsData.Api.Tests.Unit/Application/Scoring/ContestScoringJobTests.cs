using AutoFixture;

using Moq;

using SportsData.Api.Application.Jobs;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Processing;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Scoring;

public class ContestScoringJobTests : ApiTestBase<ContestScoringJob>
{
    [Fact]
    public async Task Process_Should_Enqueue_ScoreContestCommand_For_Each_Finalized_Unscored_Contest()
    {
        // Arrange
        var seasonWeekId = Guid.NewGuid();

        var currentWeek = Fixture.Build<SeasonWeek>()
            .With(x => x.Id, seasonWeekId)
            .Create();

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        // Register mock canonical data
        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetCurrentAndLastWeekSeasonWeeks())
            .ReturnsAsync([currentWeek]);

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

        // Canonical data provider returns 2 of the 3 as finalized
        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetFinalizedContestIds(seasonWeekId))
            .ReturnsAsync([contestId1, contestId2]);

        var sut = Mocker.CreateInstance<ContestScoringJob>();

        // Act
        await sut.ExecuteAsync();

        // Assert
        background.Verify(x => x.Enqueue<IScoreContests>(
            It.IsAny<Expression<Func<IScoreContests, Task>>>()), Times.Exactly(2));
    }
}
