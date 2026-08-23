using AutoFixture;

using Moq;

using SportsData.Api.Application.Jobs;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Processing;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Scoring;

public class PickScoringJobTests : ApiTestBase<PickScoringJob>
{
    // Fixed clock so the playable-window boundary is exact rather than
    // relative to wall time.
    private static readonly DateTime Now = new(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Pins the clock and returns the SUT.</summary>
    private PickScoringJob CreateSut()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
        return Mocker.CreateInstance<PickScoringJob>();
    }

    /// <summary>
    /// A contest's matchup row supplies the kickoff the job filters on.
    /// Without one the pick is invisible to the job — the join is what stops
    /// unplayed games being enqueued.
    /// </summary>
    private void SeedMatchup(Guid contestId, DateTime startDateUtc)
    {
        DataContext.PickemGroupMatchups.Add(
            Fixture.Build<PickemGroupMatchup>()
                .With(x => x.ContestId, contestId)
                .With(x => x.StartDateUtc, startDateUtc)
                .Create());
    }

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

        // All three kicked off long enough ago to be scoreable.
        SeedMatchup(contestId1, Now.AddHours(-24));
        SeedMatchup(contestId2, Now.AddHours(-24));
        SeedMatchup(contestId3, Now.AddHours(-24));

        await DataContext.SaveChangesAsync();

        // Capture every enqueued ScoreContestCommand so we can verify the exact
        // set of ContestIds, not just the call count. The count alone would miss
        // a regression where the same ContestId is enqueued twice and a distinct
        // one is dropped.
        var enqueuedCommands = new List<ScorePicksCommand>();
        var background = Mocker.GetMock<IProvideBackgroundJobs>();
        background
            .Setup(x => x.Enqueue<IScorePicks>(It.IsAny<Expression<Func<IScorePicks, Task>>>()))
            .Callback<Expression<Func<IScorePicks, Task>>>(expr =>
            {
                var cmd = ScorePicksCommandFromExpression(expr);
                if (cmd != null) enqueuedCommands.Add(cmd);
            });

        var sut = CreateSut();

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
    /// <see cref="ScorePicksCommand"/> instance. Returns null when the
    /// expression isn't shaped as expected.
    /// </summary>
    private static ScorePicksCommand? ScorePicksCommandFromExpression(
        Expression<Func<IScorePicks, Task>> expr)
    {
        if (expr.Body is not MethodCallExpression call) return null;
        if (call.Method.Name != nameof(IScorePicks.Process)) return null;
        if (call.Arguments.Count != 1) return null;

        return Expression.Lambda<Func<ScorePicksCommand>>(call.Arguments[0]).Compile()();
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

        var sut = CreateSut();

        // Act
        await sut.ExecuteAsync();

        // Assert
        background.Verify(x => x.Enqueue<IScorePicks>(
            It.IsAny<Expression<Func<IScorePicks, Task>>>()), Times.Never);
    }

    [Fact]
    public async Task Execute_SkipsContests_WhoseGameHasNotBeenPlayedYet()
    {
        // Arrange — the production symptom: a pick on a game days away. The
        // job used to enqueue it on every run, and each attempt round-tripped
        // to Producer only to be told there is no result yet.
        var futureContestId = Guid.NewGuid();
        var playedContestId = Guid.NewGuid();

        DataContext.UserPicks.AddRange(
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, futureContestId)
                .With(x => x.ScoredAt, (DateTime?)null).Create(),
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, playedContestId)
                .With(x => x.ScoredAt, (DateTime?)null).Create());

        SeedMatchup(futureContestId, Now.AddDays(4));
        SeedMatchup(playedContestId, Now.AddHours(-24));

        await DataContext.SaveChangesAsync();

        var enqueuedCommands = new List<ScorePicksCommand>();
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue<IScorePicks>(It.IsAny<Expression<Func<IScorePicks, Task>>>()))
            .Callback<Expression<Func<IScorePicks, Task>>>(expr =>
            {
                var cmd = ScorePicksCommandFromExpression(expr);
                if (cmd != null) enqueuedCommands.Add(cmd);
            });

        // Act
        await CreateSut().ExecuteAsync();

        // Assert — only the game that has actually been played.
        Assert.Single(enqueuedCommands);
        Assert.Equal(playedContestId, enqueuedCommands[0].ContestId);
    }

    [Fact]
    public async Task Execute_SkipsContests_StillInsideThePlayableWindow()
    {
        // Arrange — kicked off an hour ago. The game is very likely still in
        // progress, so scoring it is premature; the event-driven
        // ContestCompleted path handles the real finish.
        var inProgressContestId = Guid.NewGuid();

        DataContext.UserPicks.Add(
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, inProgressContestId)
                .With(x => x.ScoredAt, (DateTime?)null).Create());

        SeedMatchup(inProgressContestId, Now.AddHours(-1));

        await DataContext.SaveChangesAsync();

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        // Act
        await CreateSut().ExecuteAsync();

        // Assert
        background.Verify(x => x.Enqueue<IScorePicks>(
            It.IsAny<Expression<Func<IScorePicks, Task>>>()), Times.Never);
    }
}
