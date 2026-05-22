using AutoFixture;

using Moq;

using SportsData.Api.Application.Jobs;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Processing;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Scoring;

public class ContestScoringJobTests : ApiTestBase<ContestScoringJob>
{
    [Fact]
    public async Task Execute_EnqueuesScoreContestCommand_ForEachDistinctUnscoredContest()
    {
        // Arrange — three unscored picks spanning two distinct contests,
        // plus one already-scored pick that should NOT be enqueued.
        var contestId1 = Guid.NewGuid();
        var contestId2 = Guid.NewGuid();
        var contestId3 = Guid.NewGuid();

        DataContext.UserPicks.AddRange(
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, contestId1)
                .With(x => x.ScoredAt, (DateTime?)null).Create(),
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, contestId1) // duplicate contest — should only enqueue once
                .With(x => x.ScoredAt, (DateTime?)null).Create(),
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, contestId2)
                .With(x => x.ScoredAt, (DateTime?)null).Create(),
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, contestId3)
                .With(x => x.ScoredAt, (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)) // already scored
                .Create()
        );

        await DataContext.SaveChangesAsync();

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        var sut = Mocker.CreateInstance<ContestScoringJob>();

        // Act
        await sut.ExecuteAsync();

        // Assert — exactly two distinct unscored contests enqueued;
        // the already-scored contest is excluded by the WHERE clause.
        background.Verify(x => x.Enqueue<IScoreContests>(
            It.IsAny<Expression<Func<IScoreContests, Task>>>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Execute_DoesNothing_WhenNoUnscoredPicks()
    {
        // Arrange — only already-scored picks in the database.
        DataContext.UserPicks.Add(
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, Guid.NewGuid())
                .With(x => x.ScoredAt, (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                .Create()
        );

        await DataContext.SaveChangesAsync();

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        var sut = Mocker.CreateInstance<ContestScoringJob>();

        // Act
        await sut.ExecuteAsync();

        // Assert
        background.Verify(x => x.Enqueue<IScoreContests>(
            It.IsAny<Expression<Func<IScoreContests, Task>>>()), Times.Never);
    }
}
