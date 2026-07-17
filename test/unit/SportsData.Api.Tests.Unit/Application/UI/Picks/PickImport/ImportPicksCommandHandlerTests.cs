using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Picks.Commands.SubmitPick;
using SportsData.Api.Application.UI.Picks.PickImport.Commands.ImportPicks;
using SportsData.Api.Application.UI.Picks.PickImport.Planner;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Picks.PickImport;

public class ImportPicksCommandHandlerTests : ApiTestBase<ImportPicksCommandHandler>
{
    // Both the plan service and the (real) SubmitPickCommandHandler are driven by
    // this mocked clock, so lock state is fully deterministic regardless of the
    // calendar.
    private static readonly DateTime NowUtc = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

    // Real plan service + real submit handler so the whole write path is exercised.
    // Pass submitOverride to force per-contest submit outcomes (failure-path coverage).
    private ImportPicksCommandHandler CreateHandler(ISubmitPickCommandHandler? submitOverride = null)
    {
        var dateTime = new Mock<IDateTimeProvider>();
        dateTime.Setup(x => x.UtcNow()).Returns(NowUtc);

        var planService = new PickImportPlanService(DataContext, new PickImportPlanner(), dateTime.Object);

        var submitHandler = submitOverride ?? new SubmitPickCommandHandler(
            NullLogger<SubmitPickCommandHandler>.Instance,
            DataContext,
            new Mock<IEventBus>().Object,
            dateTime.Object);

        return new ImportPicksCommandHandler(
            NullLogger<ImportPicksCommandHandler>.Instance,
            planService,
            submitHandler);
    }

    private Guid SeedLeague(Guid userId, PickType pickType, bool useConfidence = false)
    {
        var groupId = Guid.NewGuid();
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = groupId,
            Name = "League " + groupId.ToString()[..4],
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = pickType,
            UseConfidencePoints = useConfidence,
            CommissionerUserId = userId
        });
        DataContext.PickemGroupMembers.Add(new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            PickemGroupId = groupId,
            UserId = userId,
            Role = LeagueRole.Member
        });
        return groupId;
    }

    private void SeedMatchup(Guid groupId, Guid contestId, DateTime startUtc, int week = 3)
    {
        DataContext.PickemGroupMatchups.Add(new PickemGroupMatchup
        {
            GroupId = groupId,
            SeasonWeekId = Guid.NewGuid(),
            ContestId = contestId,
            StartDateUtc = startUtc,
            SeasonYear = 2026,
            SeasonWeek = week,
            Headline = "Away @ Home",
            HomeSpread = -3.5
        });
    }

    private Guid SeedPick(Guid groupId, Guid userId, Guid contestId, Guid franchiseSeasonId)
    {
        var id = Guid.NewGuid();
        DataContext.UserPicks.Add(new PickemGroupUserPick
        {
            Id = id,
            PickemGroupId = groupId,
            UserId = userId,
            ContestId = contestId,
            Week = 3,
            PickType = PickType.StraightUp,
            FranchiseSeasonId = franchiseSeasonId,
            TiebreakerType = TiebreakerType.TotalPoints
        });
        return id;
    }

    private PickemGroupUserPick? TargetPick(Guid targetId, Guid userId, Guid contestId) =>
        DataContext.UserPicks.AsNoTracking().SingleOrDefault(p =>
            p.PickemGroupId == targetId && p.UserId == userId && p.ContestId == contestId);

    [Fact]
    public async Task ImportsNewSelections_AndStampsImportedFromPickId()
    {
        var userId = Guid.NewGuid();
        var sourceId = SeedLeague(userId, PickType.StraightUp);
        var targetId = SeedLeague(userId, PickType.StraightUp);

        var contestA = Guid.NewGuid();
        var contestB = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var future = NowUtc.AddDays(1);

        SeedMatchup(targetId, contestA, future);
        SeedMatchup(targetId, contestB, future);
        var sourcePickA = SeedPick(sourceId, userId, contestA, teamA);
        var sourcePickB = SeedPick(sourceId, userId, contestB, teamB);
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new ImportPicksCommand
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            TargetLeagueId = targetId,
            ContestIds = [contestA, contestB]
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Imported.Should().Be(2);
        result.Value.Replaced.Should().Be(0);

        var pickA = TargetPick(targetId, userId, contestA)!;
        pickA.FranchiseSeasonId.Should().Be(teamA);
        pickA.ImportedFromPickId.Should().Be(sourcePickA);
        pickA.Week.Should().Be(3);

        TargetPick(targetId, userId, contestB)!.ImportedFromPickId.Should().Be(sourcePickB);
    }

    [Fact]
    public async Task ImportsOnlySelectedContests_LeavesUnselectedUntouched()
    {
        // The user unchecks one of two importable contests — only the checked one is written.
        var userId = Guid.NewGuid();
        var sourceId = SeedLeague(userId, PickType.StraightUp);
        var targetId = SeedLeague(userId, PickType.StraightUp);

        var selected = Guid.NewGuid();
        var unselected = Guid.NewGuid();
        var team = Guid.NewGuid();
        var future = NowUtc.AddDays(1);

        SeedMatchup(targetId, selected, future);
        SeedMatchup(targetId, unselected, future);
        SeedPick(sourceId, userId, selected, team);
        SeedPick(sourceId, userId, unselected, team);
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new ImportPicksCommand
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            TargetLeagueId = targetId,
            ContestIds = [selected]
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Imported.Should().Be(1);
        result.Value.SkippedByReason.Should().ContainKey("NotSelected").WhoseValue.Should().Be(1);

        TargetPick(targetId, userId, selected).Should().NotBeNull();
        TargetPick(targetId, userId, unselected).Should().BeNull();
    }

    [Fact]
    public async Task NullContestIds_ImportsNothing_WithoutThrowing()
    {
        // A "contestIds": null payload overrides the DTO default; treat it as an
        // empty selection (no-op) rather than throwing.
        var userId = Guid.NewGuid();
        var sourceId = SeedLeague(userId, PickType.StraightUp);
        var targetId = SeedLeague(userId, PickType.StraightUp);

        var contest = Guid.NewGuid();
        var team = Guid.NewGuid();
        SeedMatchup(targetId, contest, NowUtc.AddDays(1));
        SeedPick(sourceId, userId, contest, team);
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new ImportPicksCommand
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            TargetLeagueId = targetId,
            ContestIds = null!
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Imported.Should().Be(0);
        TargetPick(targetId, userId, contest).Should().BeNull();
    }

    [Fact]
    public async Task ReplacesOnlySelectedCollisions_AndLeavesUnselected()
    {
        var userId = Guid.NewGuid();
        var sourceId = SeedLeague(userId, PickType.StraightUp);
        var targetId = SeedLeague(userId, PickType.StraightUp);

        var approved = Guid.NewGuid();
        var kept = Guid.NewGuid();
        var sourceTeam = Guid.NewGuid();
        var existingApproved = Guid.NewGuid();
        var existingKept = Guid.NewGuid();
        var future = NowUtc.AddDays(1);

        SeedMatchup(targetId, approved, future);
        SeedMatchup(targetId, kept, future);
        var sourcePickApproved = SeedPick(sourceId, userId, approved, sourceTeam);
        SeedPick(sourceId, userId, kept, sourceTeam);
        SeedPick(targetId, userId, approved, existingApproved);
        SeedPick(targetId, userId, kept, existingKept);
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new ImportPicksCommand
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            TargetLeagueId = targetId,
            ContestIds = [approved]
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Imported.Should().Be(0);
        result.Value.Replaced.Should().Be(1);
        result.Value.SkippedByReason.Should().ContainKey("NotSelected").WhoseValue.Should().Be(1);

        // Approved collision was overwritten with the source team + traceability.
        var approvedPick = TargetPick(targetId, userId, approved)!;
        approvedPick.FranchiseSeasonId.Should().Be(sourceTeam);
        approvedPick.ImportedFromPickId.Should().Be(sourcePickApproved);

        // Kept collision is untouched.
        var keptPick = TargetPick(targetId, userId, kept)!;
        keptPick.FranchiseSeasonId.Should().Be(existingKept);
        keptPick.ImportedFromPickId.Should().BeNull();
    }

    [Fact]
    public async Task SkipsLockedMatchesAndNotShared_WithReasons()
    {
        var userId = Guid.NewGuid();
        var sourceId = SeedLeague(userId, PickType.StraightUp);
        var targetId = SeedLeague(userId, PickType.StraightUp);

        var locked = Guid.NewGuid();
        var matches = Guid.NewGuid();
        var notShared = Guid.NewGuid();
        var team = Guid.NewGuid();

        SeedMatchup(targetId, locked, NowUtc.AddDays(-1));
        SeedMatchup(targetId, matches, NowUtc.AddDays(1));
        SeedMatchup(targetId, notShared, NowUtc.AddDays(1));
        SeedPick(sourceId, userId, locked, team);
        SeedPick(sourceId, userId, matches, team);
        SeedPick(targetId, userId, matches, team); // already matches
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new ImportPicksCommand
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            TargetLeagueId = targetId,
            ContestIds = [locked, matches, notShared]
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Imported.Should().Be(0);
        result.Value.SkippedByReason.Should().ContainKey(nameof(PickImportSkipReason.Locked));
        result.Value.SkippedByReason.Should().ContainKey(nameof(PickImportSkipReason.AlreadyMatches));
        result.Value.SkippedByReason.Should().ContainKey(nameof(PickImportSkipReason.NotShared));

        // Locked contest was never written.
        TargetPick(targetId, userId, locked).Should().BeNull();
    }

    [Fact]
    public async Task ReturnsDraftForConfidenceTarget_WithoutWriting()
    {
        var userId = Guid.NewGuid();
        var sourceId = SeedLeague(userId, PickType.StraightUp);
        var targetId = SeedLeague(userId, PickType.StraightUp, useConfidence: true);

        var contest = Guid.NewGuid();
        var team = Guid.NewGuid();
        SeedMatchup(targetId, contest, NowUtc.AddDays(1));
        SeedPick(sourceId, userId, contest, team);
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new ImportPicksCommand
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            TargetLeagueId = targetId,
            ContestIds = [contest]
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresConfidence.Should().BeTrue();
        result.Value.Imported.Should().Be(0);
        result.Value.Draft.Should().ContainSingle(d => d.ContestId == contest && d.FranchiseSeasonId == team);

        // Confidence targets never write from the import; the pick sheet persists later.
        TargetPick(targetId, userId, contest).Should().BeNull();
    }

    [Fact]
    public async Task ConfidenceDraft_IncludesApprovedReplaceSelections_ButKeepsExistingPicks()
    {
        var userId = Guid.NewGuid();
        var sourceId = SeedLeague(userId, PickType.StraightUp);
        var targetId = SeedLeague(userId, PickType.StraightUp, useConfidence: true);

        var collision = Guid.NewGuid();
        var sourceTeam = Guid.NewGuid();
        var existingTeam = Guid.NewGuid();
        SeedMatchup(targetId, collision, NowUtc.AddDays(1));
        SeedPick(sourceId, userId, collision, sourceTeam);
        SeedPick(targetId, userId, collision, existingTeam);
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new ImportPicksCommand
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            TargetLeagueId = targetId,
            ContestIds = [collision]
        });

        result.Value.RequiresConfidence.Should().BeTrue();
        result.Value.Draft.Should().ContainSingle(d => d.ContestId == collision && d.FranchiseSeasonId == sourceTeam);

        // Draft mode writes nothing — the existing target pick is untouched.
        TargetPick(targetId, userId, collision)!.FranchiseSeasonId.Should().Be(existingTeam);
    }

    [Fact]
    public async Task PropagatesPlanFailure_WhenNotMemberOfSource()
    {
        var userId = Guid.NewGuid();
        var targetId = SeedLeague(userId, PickType.StraightUp);
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new ImportPicksCommand
        {
            UserId = userId,
            SourceLeagueId = Guid.NewGuid(), // not a member
            TargetLeagueId = targetId
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task CountsSubmitFailureUnderFailed_AndImportsTheRest()
    {
        // A contest can lock (or otherwise fail to submit) between plan and commit.
        // Force a failure on one of two planned imports; the other goes through the
        // real submit handler and actually persists, and the failure is counted,
        // not fatal.
        var userId = Guid.NewGuid();
        var sourceId = SeedLeague(userId, PickType.StraightUp);
        var targetId = SeedLeague(userId, PickType.StraightUp);

        var okContest = Guid.NewGuid();
        var failContest = Guid.NewGuid();
        var team = Guid.NewGuid();
        var future = NowUtc.AddDays(1);

        SeedMatchup(targetId, okContest, future);
        SeedMatchup(targetId, failContest, future);
        SeedPick(sourceId, userId, okContest, team);
        SeedPick(sourceId, userId, failContest, team);
        await DataContext.SaveChangesAsync();

        var realSubmitClock = new Mock<IDateTimeProvider>();
        realSubmitClock.Setup(x => x.UtcNow()).Returns(NowUtc);
        var realSubmit = new SubmitPickCommandHandler(
            NullLogger<SubmitPickCommandHandler>.Instance,
            DataContext,
            new Mock<IEventBus>().Object,
            realSubmitClock.Object);

        var submit = new Mock<ISubmitPickCommandHandler>();
        submit.Setup(x => x.ExecuteAsync(
                It.Is<SubmitPickCommand>(c => c.ContestId == failContest), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<Guid>(Guid.Empty, ResultStatus.Validation, []));
        // okContest delegates to the real handler so it genuinely persists.
        submit.Setup(x => x.ExecuteAsync(
                It.Is<SubmitPickCommand>(c => c.ContestId == okContest), It.IsAny<CancellationToken>()))
            .Returns((SubmitPickCommand c, CancellationToken ct) => realSubmit.ExecuteAsync(c, ct));

        var result = await CreateHandler(submit.Object).ExecuteAsync(new ImportPicksCommand
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            TargetLeagueId = targetId,
            ContestIds = [okContest, failContest]
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Imported.Should().Be(1);
        result.Value.SkippedByReason.Should().ContainKey("Failed").WhoseValue.Should().Be(1);
        result.Value.Skipped.Should().Be(1);

        // The successful import actually persisted; the failed one did not.
        var okPick = TargetPick(targetId, userId, okContest);
        okPick.Should().NotBeNull();
        okPick!.FranchiseSeasonId.Should().Be(team);
        TargetPick(targetId, userId, failContest).Should().BeNull();
    }

    [Fact]
    public async Task IsIdempotent_WhenReRun()
    {
        var userId = Guid.NewGuid();
        var sourceId = SeedLeague(userId, PickType.StraightUp);
        var targetId = SeedLeague(userId, PickType.StraightUp);

        var contest = Guid.NewGuid();
        var team = Guid.NewGuid();
        SeedMatchup(targetId, contest, NowUtc.AddDays(1));
        SeedPick(sourceId, userId, contest, team);
        await DataContext.SaveChangesAsync();

        var command = new ImportPicksCommand
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            TargetLeagueId = targetId,
            ContestIds = [contest]
        };

        var first = await CreateHandler().ExecuteAsync(command);
        first.Value.Imported.Should().Be(1);

        // Second run: the target now matches the source, so it's a no-op skip.
        var second = await CreateHandler().ExecuteAsync(command);
        second.Value.Imported.Should().Be(0);
        second.Value.SkippedByReason.Should().ContainKey(nameof(PickImportSkipReason.AlreadyMatches));

        DataContext.UserPicks.AsNoTracking()
            .Count(p => p.PickemGroupId == targetId && p.ContestId == contest)
            .Should().Be(1);
    }
}
