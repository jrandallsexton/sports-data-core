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
    private static readonly DateTime NowUtc = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

    // Real plan service + real submit handler so the whole write path is exercised.
    private ImportPicksCommandHandler CreateHandler()
    {
        var dateTime = new Mock<IDateTimeProvider>();
        dateTime.Setup(x => x.UtcNow()).Returns(NowUtc);

        var planService = new PickImportPlanService(DataContext, new PickImportPlanner(), dateTime.Object);

        var submitHandler = new SubmitPickCommandHandler(
            NullLogger<SubmitPickCommandHandler>.Instance,
            DataContext,
            new Mock<IEventBus>().Object);

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
            TargetLeagueId = targetId
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
    public async Task ReplacesOnlyApprovedCollisions_AndKeepsTheRest()
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
            ReplaceContestIds = [approved]
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Imported.Should().Be(0);
        result.Value.Replaced.Should().Be(1);
        result.Value.SkippedByReason.Should().ContainKey("KeptExisting").WhoseValue.Should().Be(1);

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
            TargetLeagueId = targetId
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
    public async Task RejectsConfidencePointsTarget()
    {
        var userId = Guid.NewGuid();
        var sourceId = SeedLeague(userId, PickType.StraightUp);
        var targetId = SeedLeague(userId, PickType.StraightUp, useConfidence: true);
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new ImportPicksCommand
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            TargetLeagueId = targetId
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
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
            TargetLeagueId = targetId
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
