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

public class PickScoringAuditJobTests : ApiTestBase<PickScoringAuditJob>
{
    [Fact]
    public async Task Execute_EnqueuesAuditCommand_ForEachDistinctScoredContestOfThisSport()
    {
        // Arrange — five scored picks: two distinct NCAAFB contests
        // (one with a duplicate to verify Distinct), one NFL contest, one MLB
        // contest, and one NCAAFB pick that is UNSCORED. Audit should enqueue
        // exactly the two distinct NCAAFB scored contests.
        var ncaaContest1 = Guid.NewGuid();
        var ncaaContest2 = Guid.NewGuid();
        var nflContest = Guid.NewGuid();
        var mlbContest = Guid.NewGuid();
        var ncaaUnscoredContest = Guid.NewGuid();

        var ncaaGroup = Fixture.Build<PickemGroup>()
            .With(g => g.Sport, Sport.FootballNcaa).Create();
        var nflGroup = Fixture.Build<PickemGroup>()
            .With(g => g.Sport, Sport.FootballNfl).Create();
        var mlbGroup = Fixture.Build<PickemGroup>()
            .With(g => g.Sport, Sport.BaseballMlb).Create();

        DataContext.PickemGroups.AddRange(ncaaGroup, nflGroup, mlbGroup);

        var scoredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Explicitly set Group navigation to the seeded instance. Without
        // this, AutoFixture creates a *new* PickemGroup for each pick's
        // navigation property and EF cascade-inserts it, overriding the FK.
        DataContext.UserPicks.AddRange(
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, ncaaContest1)
                .With(x => x.PickemGroupId, ncaaGroup.Id)
                .With(x => x.Group, ncaaGroup)
                .With(x => x.ScoredAt, (DateTime?)scoredAt).Create(),
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, ncaaContest1) // duplicate contest, same sport
                .With(x => x.PickemGroupId, ncaaGroup.Id)
                .With(x => x.Group, ncaaGroup)
                .With(x => x.ScoredAt, (DateTime?)scoredAt).Create(),
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, ncaaContest2)
                .With(x => x.PickemGroupId, ncaaGroup.Id)
                .With(x => x.Group, ncaaGroup)
                .With(x => x.ScoredAt, (DateTime?)scoredAt).Create(),
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, nflContest)
                .With(x => x.PickemGroupId, nflGroup.Id)
                .With(x => x.Group, nflGroup)
                .With(x => x.ScoredAt, (DateTime?)scoredAt).Create(),
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, mlbContest)
                .With(x => x.PickemGroupId, mlbGroup.Id)
                .With(x => x.Group, mlbGroup)
                .With(x => x.ScoredAt, (DateTime?)scoredAt).Create(),
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, ncaaUnscoredContest)
                .With(x => x.PickemGroupId, ncaaGroup.Id)
                .With(x => x.Group, ncaaGroup)
                .With(x => x.ScoredAt, (DateTime?)null).Create()
        );

        await DataContext.SaveChangesAsync();

        var enqueuedCommands = new List<AuditContestCommand>();
        var background = Mocker.GetMock<IProvideBackgroundJobs>();
        background
            .Setup(x => x.Enqueue<IPickScoringAudit>(It.IsAny<Expression<Func<IPickScoringAudit, Task>>>()))
            .Callback<Expression<Func<IPickScoringAudit, Task>>>(expr =>
            {
                var cmd = AuditContestCommandFromExpression(expr);
                if (cmd != null) enqueuedCommands.Add(cmd);
            });

        var sut = Mocker.CreateInstance<PickScoringAuditJob>();

        // Act
        await sut.ExecuteAsync(Sport.FootballNcaa);

        // Assert — exactly two enqueues, only the NCAAFB scored contests.
        Assert.Equal(2, enqueuedCommands.Count);
        Assert.Equal(
            new HashSet<Guid> { ncaaContest1, ncaaContest2 },
            enqueuedCommands.Select(c => c.ContestId).ToHashSet());
        Assert.All(enqueuedCommands, c => Assert.Equal(Sport.FootballNcaa, c.Sport));
    }

    [Fact]
    public async Task Execute_EnqueuesNothing_WhenSportHasNoScoredPicks()
    {
        // Arrange — only MLB picks scored; audit fires for NCAAFB.
        var mlbGroup = Fixture.Build<PickemGroup>()
            .With(g => g.Sport, Sport.BaseballMlb).Create();
        DataContext.PickemGroups.Add(mlbGroup);

        DataContext.UserPicks.Add(
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, Guid.NewGuid())
                .With(x => x.PickemGroupId, mlbGroup.Id)
                .With(x => x.Group, mlbGroup)
                .With(x => x.ScoredAt, (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                .Create()
        );

        await DataContext.SaveChangesAsync();

        var background = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<PickScoringAuditJob>();

        await sut.ExecuteAsync(Sport.FootballNcaa);

        background.Verify(x => x.Enqueue<IPickScoringAudit>(
            It.IsAny<Expression<Func<IPickScoringAudit, Task>>>()), Times.Never);
    }

    [Fact]
    public async Task Execute_SkipsUnscoredPicks_EvenForTargetSport()
    {
        // Arrange — NCAAFB pick exists but ScoredAt = null. PickScoringJob's
        // territory, not the audit's.
        var ncaaGroup = Fixture.Build<PickemGroup>()
            .With(g => g.Sport, Sport.FootballNcaa).Create();
        DataContext.PickemGroups.Add(ncaaGroup);

        DataContext.UserPicks.Add(
            Fixture.Build<PickemGroupUserPick>()
                .With(x => x.ContestId, Guid.NewGuid())
                .With(x => x.PickemGroupId, ncaaGroup.Id)
                .With(x => x.Group, ncaaGroup)
                .With(x => x.ScoredAt, (DateTime?)null).Create()
        );

        await DataContext.SaveChangesAsync();

        var background = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<PickScoringAuditJob>();

        await sut.ExecuteAsync(Sport.FootballNcaa);

        background.Verify(x => x.Enqueue<IPickScoringAudit>(
            It.IsAny<Expression<Func<IPickScoringAudit, Task>>>()), Times.Never);
    }

    private static AuditContestCommand? AuditContestCommandFromExpression(
        Expression<Func<IPickScoringAudit, Task>> expr)
    {
        if (expr.Body is not MethodCallExpression call) return null;
        if (call.Method.Name != nameof(IPickScoringAudit.Process)) return null;
        if (call.Arguments.Count != 1) return null;

        return Expression.Lambda<Func<AuditContestCommand>>(call.Arguments[0]).Compile()();
    }
}
