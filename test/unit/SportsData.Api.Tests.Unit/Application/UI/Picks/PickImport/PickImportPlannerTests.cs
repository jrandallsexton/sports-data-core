using FluentAssertions;

using SportsData.Api.Application.UI.Leagues.PickImport.Planner;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.PickImport;

public class PickImportPlannerTests
{
    private readonly PickImportPlanner _planner = new();

    private static PickImportTargetMatchup Matchup(Guid contestId, bool isLocked = false, int week = 1) =>
        new(contestId, week, isLocked, Headline: "Away @ Home", HomeSpread: -3.5);

    private static PickImportPlanInput Input(
        IReadOnlyCollection<PickImportTargetMatchup> matchups,
        IReadOnlyDictionary<Guid, PickImportSourceSelection> source,
        IReadOnlyDictionary<Guid, Guid> existing) =>
        new(matchups, source, existing);

    [Fact]
    public void BuildPlan_ImportsSourceSelection_WhenNoExistingTargetPick()
    {
        var contestId = Guid.NewGuid();
        var team = Guid.NewGuid();
        var sourcePickId = Guid.NewGuid();

        var plan = _planner.BuildPlan(Input(
            [Matchup(contestId)],
            new Dictionary<Guid, PickImportSourceSelection> { [contestId] = new(team, sourcePickId) },
            new Dictionary<Guid, Guid>()));

        plan.ToImport.Should().ContainSingle();
        plan.ToImport[0].ContestId.Should().Be(contestId);
        plan.ToImport[0].FranchiseSeasonId.Should().Be(team);
        plan.ToImport[0].SourcePickId.Should().Be(sourcePickId);
        plan.Collisions.Should().BeEmpty();
        plan.Skipped.Should().BeEmpty();
    }

    [Fact]
    public void BuildPlan_SkipsAsAlreadyMatches_WhenExistingEqualsSource()
    {
        var contestId = Guid.NewGuid();
        var team = Guid.NewGuid();

        var plan = _planner.BuildPlan(Input(
            [Matchup(contestId)],
            new Dictionary<Guid, PickImportSourceSelection> { [contestId] = new(team, Guid.NewGuid()) },
            new Dictionary<Guid, Guid> { [contestId] = team }));

        plan.Skipped.Should().ContainSingle();
        plan.Skipped[0].Reason.Should().Be(PickImportSkipReason.AlreadyMatches);
        plan.ToImport.Should().BeEmpty();
        plan.Collisions.Should().BeEmpty();
    }

    [Fact]
    public void BuildPlan_RecordsCollision_WhenExistingDiffersFromSource()
    {
        var contestId = Guid.NewGuid();
        var sourceTeam = Guid.NewGuid();
        var existingTeam = Guid.NewGuid();
        var sourcePickId = Guid.NewGuid();

        var plan = _planner.BuildPlan(Input(
            [Matchup(contestId)],
            new Dictionary<Guid, PickImportSourceSelection> { [contestId] = new(sourceTeam, sourcePickId) },
            new Dictionary<Guid, Guid> { [contestId] = existingTeam }));

        plan.Collisions.Should().ContainSingle();
        plan.Collisions[0].SourceFranchiseSeasonId.Should().Be(sourceTeam);
        plan.Collisions[0].ExistingFranchiseSeasonId.Should().Be(existingTeam);
        plan.Collisions[0].SourcePickId.Should().Be(sourcePickId);
        plan.ToImport.Should().BeEmpty();
        plan.Skipped.Should().BeEmpty();
    }

    [Fact]
    public void BuildPlan_SkipsAsNotShared_WhenNoSourcePick()
    {
        var contestId = Guid.NewGuid();

        var plan = _planner.BuildPlan(Input(
            [Matchup(contestId)],
            new Dictionary<Guid, PickImportSourceSelection>(),
            new Dictionary<Guid, Guid>()));

        plan.Skipped.Should().ContainSingle();
        plan.Skipped[0].Reason.Should().Be(PickImportSkipReason.NotShared);
        plan.ToImport.Should().BeEmpty();
        plan.Collisions.Should().BeEmpty();
    }

    [Fact]
    public void BuildPlan_SkipsAsLocked_WhenTargetMatchupLocked()
    {
        var contestId = Guid.NewGuid();
        var team = Guid.NewGuid();

        var plan = _planner.BuildPlan(Input(
            [Matchup(contestId, isLocked: true)],
            new Dictionary<Guid, PickImportSourceSelection> { [contestId] = new(team, Guid.NewGuid()) },
            new Dictionary<Guid, Guid>()));

        plan.Skipped.Should().ContainSingle();
        plan.Skipped[0].Reason.Should().Be(PickImportSkipReason.Locked);
        plan.ToImport.Should().BeEmpty();
    }

    [Fact]
    public void BuildPlan_LockedTakesPrecedenceOverCollision()
    {
        // A locked target matchup is skipped even when the user has a differing
        // existing pick — it can't be changed, so it's never a collision to resolve.
        var contestId = Guid.NewGuid();

        var plan = _planner.BuildPlan(Input(
            [Matchup(contestId, isLocked: true)],
            new Dictionary<Guid, PickImportSourceSelection> { [contestId] = new(Guid.NewGuid(), Guid.NewGuid()) },
            new Dictionary<Guid, Guid> { [contestId] = Guid.NewGuid() }));

        plan.Skipped.Should().ContainSingle();
        plan.Skipped[0].Reason.Should().Be(PickImportSkipReason.Locked);
        plan.Collisions.Should().BeEmpty();
    }

    [Fact]
    public void BuildPlan_ClassifiesEachContestIndependently()
    {
        var toImport = Guid.NewGuid();
        var matches = Guid.NewGuid();
        var collides = Guid.NewGuid();
        var notShared = Guid.NewGuid();
        var team = Guid.NewGuid();

        var plan = _planner.BuildPlan(Input(
            [Matchup(toImport), Matchup(matches), Matchup(collides), Matchup(notShared)],
            new Dictionary<Guid, PickImportSourceSelection>
            {
                [toImport] = new(team, Guid.NewGuid()),
                [matches] = new(team, Guid.NewGuid()),
                [collides] = new(team, Guid.NewGuid())
            },
            new Dictionary<Guid, Guid>
            {
                [matches] = team,
                [collides] = Guid.NewGuid()
            }));

        plan.ToImport.Should().ContainSingle(i => i.ContestId == toImport);
        plan.Collisions.Should().ContainSingle(c => c.ContestId == collides);
        plan.Skipped.Should().Contain(s => s.ContestId == matches && s.Reason == PickImportSkipReason.AlreadyMatches);
        plan.Skipped.Should().Contain(s => s.ContestId == notShared && s.Reason == PickImportSkipReason.NotShared);
    }
}
