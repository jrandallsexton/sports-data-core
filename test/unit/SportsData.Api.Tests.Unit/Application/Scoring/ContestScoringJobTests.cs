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

        // Capture every enqueued ScoreContestCommand so we can verify the exact
        // set of ContestIds, not just the call count. The count alone would miss
        // a regression where the same ContestId is enqueued twice and a distinct
        // one is dropped.
        var enqueuedCommands = new List<ScoreContestCommand>();
        var background = Mocker.GetMock<IProvideBackgroundJobs>();
        background
            .Setup(x => x.Enqueue<IScoreContests>(It.IsAny<Expression<Func<IScoreContests, Task>>>()))
            .Callback<Expression<Func<IScoreContests, Task>>>(expr =>
            {
                var cmd = ScoreContestCommandFromExpression(expr);
                if (cmd != null) enqueuedCommands.Add(cmd);
            });

        var sut = Mocker.CreateInstance<ContestScoringJob>();

        // Act
        await sut.ExecuteAsync();

        // Assert — exactly two enqueues, one per distinct unscored contest.
        Assert.Equal(2, enqueuedCommands.Count);
        Assert.Equal(
            new HashSet<Guid> { contestId1, contestId2 },
            enqueuedCommands.Select(c => c.ContestId).ToHashSet());
    }

    /// <summary>
    /// Compiles and evaluates the single argument of a
    /// <c>p =&gt; p.Process(cmd)</c> expression to extract the captured
    /// <see cref="ScoreContestCommand"/> instance. Returns null when the
    /// expression isn't shaped as expected.
    /// </summary>
    private static ScoreContestCommand? ScoreContestCommandFromExpression(
        Expression<Func<IScoreContests, Task>> expr)
    {
        if (expr.Body is not MethodCallExpression call) return null;
        if (call.Method.Name != nameof(IScoreContests.Process)) return null;
        if (call.Arguments.Count != 1) return null;

        return Expression.Lambda<Func<ScoreContestCommand>>(call.Arguments[0]).Compile()();
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
