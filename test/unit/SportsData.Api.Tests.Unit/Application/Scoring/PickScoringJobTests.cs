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

    /// <summary>Mirrors PickScoringJob.PlayableWindowHours.</summary>
    private const int PlayableWindowHours = 4;

    /// <summary>Pins the clock and returns the SUT.</summary>
    private PickScoringJob CreateSut()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
        return Mocker.CreateInstance<PickScoringJob>();
    }

    /// <summary>
    /// A contest's matchup row supplies the kickoff the job filters on.
    /// Without one the pick is invisible to the job — the join is what stops
    /// unplayed games being enqueued. The row is keyed to the SAME group as
    /// the pick, because the job joins on group AND contest.
    /// </summary>
    private void SeedMatchup(Guid contestId, DateTime startDateUtc, Guid groupId)
    {
        // OmitAutoProperties is load-bearing: Build<T> otherwise populates
        // navigation properties, and EF persists the whole generated graph —
        // seeding one matchup produced 37 rows with random 2027 kickoffs, and
        // the pick's PickemGroupId was overwritten by its generated nav.
        DataContext.PickemGroupMatchups.Add(
            Fixture.Build<PickemGroupMatchup>()
                .OmitAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.ContestId, contestId)
                .With(x => x.GroupId, groupId)
                .With(x => x.StartDateUtc, startDateUtc)
                .Create());
    }

    /// <summary>Seeds an unscored pick plus its group's matchup row.</summary>
    private Guid SeedUnscoredPick(Guid contestId, DateTime startDateUtc)
    {
        var groupId = Guid.NewGuid();

        DataContext.UserPicks.Add(
            Fixture.Build<PickemGroupUserPick>()
                .OmitAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.ContestId, contestId)
                .With(x => x.PickemGroupId, groupId)
                .With(x => x.ScoredAt, (DateTime?)null)
                .Create());

        SeedMatchup(contestId, startDateUtc, groupId);
        return groupId;
    }

    [Fact]
    public async Task Execute_EnqueuesScoreContestCommand_ForEachDistinctUnscoredContest()
    {
        // Arrange — three unscored picks spanning two distinct contests,
        // plus one already-scored pick that should NOT be enqueued.
        var contestId1 = Guid.NewGuid();
        var contestId2 = Guid.NewGuid();
        var contestId3 = Guid.NewGuid();

        // All kicked off long enough ago to be scoreable.
        SeedUnscoredPick(contestId1, Now.AddHours(-24));
        SeedUnscoredPick(contestId2, Now.AddHours(-24));

        // Same contest picked in a SECOND group — must still enqueue once.
        // With a contest-only join this pick would also fan out across the
        // other group's matchup row.
        SeedUnscoredPick(contestId1, Now.AddHours(-24));

        // Already scored — never enqueued.
        var scoredGroupId = Guid.NewGuid();
        DataContext.UserPicks.Add(
            Fixture.Build<PickemGroupUserPick>()
                .OmitAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.ContestId, contestId3)
                .With(x => x.PickemGroupId, scoredGroupId)
                .With(x => x.ScoredAt, (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                .Create());
        SeedMatchup(contestId3, Now.AddHours(-24), scoredGroupId);

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
                .OmitAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
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

        SeedUnscoredPick(futureContestId, Now.AddDays(4));
        SeedUnscoredPick(playedContestId, Now.AddHours(-24));

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
        // Arrange — kicked off three hours ago, so inside the four-hour
        // window: an NFL game of that age may still be running. Scoring it is
        // premature; the event-driven ContestCompleted path handles the real
        // finish. Paired with the test below, this pins the window rather
        // than merely passing for any generous value.
        var inProgressContestId = Guid.NewGuid();

        SeedUnscoredPick(inProgressContestId, Now.AddHours(-3));

        await DataContext.SaveChangesAsync();

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        // Act
        await CreateSut().ExecuteAsync();

        // Assert
        background.Verify(x => x.Enqueue<IScorePicks>(
            It.IsAny<Expression<Func<IScorePicks, Task>>>()), Times.Never);
    }

    [Fact]
    public async Task Execute_ScoresContests_ExactlyAtThePlayableWindow()
    {
        // Arrange — kicked off exactly four hours ago. The filter is
        // inclusive, so the boundary itself qualifies. With the 3h and 5h
        // cases either side, the cutoff is pinned to the hour: a drift to
        // 4.5 would break this test.
        var boundaryContestId = Guid.NewGuid();
        SeedUnscoredPick(boundaryContestId, Now.AddHours(-PlayableWindowHours));
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

        // Assert
        Assert.Single(enqueuedCommands);
        Assert.Equal(boundaryContestId, enqueuedCommands[0].ContestId);
    }

    [Fact]
    public async Task Execute_IgnoresAnotherGroupsMatchupRow()
    {
        // Arrange — one group picked the contest; a DIFFERENT group carries a
        // matchup row for the same contest with a stale, much older kickoff
        // (e.g. left behind by a reschedule). Joining on contest alone would
        // let that row admit this pick before its own game has been played.
        var contestId = Guid.NewGuid();
        SeedUnscoredPick(contestId, Now.AddHours(-1)); // this group: still in progress
        SeedMatchup(contestId, Now.AddDays(-3), Guid.NewGuid()); // another group: stale

        await DataContext.SaveChangesAsync();

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        // Act
        await CreateSut().ExecuteAsync();

        // Assert — the pick is gated by ITS OWN group's row.
        background.Verify(x => x.Enqueue<IScorePicks>(
            It.IsAny<Expression<Func<IScorePicks, Task>>>()), Times.Never);
    }

    [Fact]
    public async Task Execute_ScoresContests_JustPastThePlayableWindow()
    {
        // Arrange — kicked off five hours ago: past four, so even an
        // overtime game is done and the contest is worth an attempt.
        var finishedContestId = Guid.NewGuid();

        SeedUnscoredPick(finishedContestId, Now.AddHours(-5));

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

        // Assert
        Assert.Single(enqueuedCommands);
        Assert.Equal(finishedContestId, enqueuedCommands[0].ContestId);
    }
}
