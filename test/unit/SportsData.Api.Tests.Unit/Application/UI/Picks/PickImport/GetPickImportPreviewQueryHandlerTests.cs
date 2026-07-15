using FluentAssertions;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Picks.PickImport.Planner;
using SportsData.Api.Application.UI.Picks.PickImport.Queries.GetPickImportPreview;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Picks.PickImport;

public class GetPickImportPreviewQueryHandlerTests : ApiTestBase<GetPickImportPreviewQueryHandler>
{
    private static readonly DateTime NowUtc = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

    private GetPickImportPreviewQueryHandler CreateHandler()
    {
        var dateTime = new Mock<IDateTimeProvider>();
        dateTime.Setup(x => x.UtcNow()).Returns(NowUtc);
        return new GetPickImportPreviewQueryHandler(DataContext, new PickImportPlanner(), dateTime.Object);
    }

    private Guid SeedLeague(Guid userId, PickType pickType, bool useConfidence = false, bool deactivated = false)
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
            DeactivatedUtc = deactivated ? NowUtc : null,
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

    private void SeedMatchup(Guid groupId, Guid contestId, DateTime startUtc, int week = 1)
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

    private void SeedPick(Guid groupId, Guid userId, Guid contestId, Guid? franchiseSeasonId, PickType pickType = PickType.StraightUp)
    {
        DataContext.UserPicks.Add(new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = groupId,
            UserId = userId,
            ContestId = contestId,
            Week = 1,
            PickType = pickType,
            FranchiseSeasonId = franchiseSeasonId,
            TiebreakerType = TiebreakerType.TotalPoints
        });
    }

    [Fact]
    public async Task ReturnsValidation_WhenSourceEqualsTarget()
    {
        var leagueId = Guid.NewGuid();
        var query = new GetPickImportPreviewQuery
        {
            UserId = Guid.NewGuid(),
            SourceLeagueId = leagueId,
            TargetLeagueId = leagueId
        };

        var result = await CreateHandler().ExecuteAsync(query);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task ReturnsNotFound_WhenNotMemberOfTarget()
    {
        var userId = Guid.NewGuid();
        var sourceId = SeedLeague(userId, PickType.StraightUp);
        await DataContext.SaveChangesAsync();

        var query = new GetPickImportPreviewQuery
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            TargetLeagueId = Guid.NewGuid() // not a member
        };

        var result = await CreateHandler().ExecuteAsync(query);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ReturnsNotFound_WhenNotMemberOfSource()
    {
        var userId = Guid.NewGuid();
        var targetId = SeedLeague(userId, PickType.StraightUp);
        await DataContext.SaveChangesAsync();

        var query = new GetPickImportPreviewQuery
        {
            UserId = userId,
            SourceLeagueId = Guid.NewGuid(), // not a member
            TargetLeagueId = targetId
        };

        var result = await CreateHandler().ExecuteAsync(query);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ReturnsValidation_WhenPickTypesDiffer()
    {
        var userId = Guid.NewGuid();
        var sourceId = SeedLeague(userId, PickType.StraightUp);
        var targetId = SeedLeague(userId, PickType.AgainstTheSpread);
        await DataContext.SaveChangesAsync();

        var query = new GetPickImportPreviewQuery
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            TargetLeagueId = targetId
        };

        var result = await CreateHandler().ExecuteAsync(query);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task ClassifiesImportCollisionAndSkips_ForSharedContests()
    {
        var userId = Guid.NewGuid();
        var sourceId = SeedLeague(userId, PickType.StraightUp);
        var targetId = SeedLeague(userId, PickType.StraightUp);

        var importContest = Guid.NewGuid();
        var collisionContest = Guid.NewGuid();
        var matchesContest = Guid.NewGuid();
        var lockedContest = Guid.NewGuid();
        var notSharedContest = Guid.NewGuid();

        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        var future = NowUtc.AddDays(1);
        var past = NowUtc.AddDays(-1);

        // Target universe
        SeedMatchup(targetId, importContest, future);
        SeedMatchup(targetId, collisionContest, future);
        SeedMatchup(targetId, matchesContest, future);
        SeedMatchup(targetId, lockedContest, past);
        SeedMatchup(targetId, notSharedContest, future);

        // Source picks
        SeedPick(sourceId, userId, importContest, teamA);
        SeedPick(sourceId, userId, collisionContest, teamA);
        SeedPick(sourceId, userId, matchesContest, teamA);
        SeedPick(sourceId, userId, lockedContest, teamA);
        // note: no source pick for notSharedContest

        // Existing target picks
        SeedPick(targetId, userId, collisionContest, teamB); // differs → collision
        SeedPick(targetId, userId, matchesContest, teamA);   // matches → skip

        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new GetPickImportPreviewQuery
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            TargetLeagueId = targetId
        });

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;

        dto.ToImport.Should().ContainSingle(i => i.ContestId == importContest && i.FranchiseSeasonId == teamA);
        dto.Collisions.Should().ContainSingle(c =>
            c.ContestId == collisionContest &&
            c.SourceFranchiseSeasonId == teamA &&
            c.ExistingFranchiseSeasonId == teamB);
        dto.Skipped.Should().Contain(s => s.ContestId == matchesContest && s.Reason == nameof(PickImportSkipReason.AlreadyMatches));
        dto.Skipped.Should().Contain(s => s.ContestId == lockedContest && s.Reason == nameof(PickImportSkipReason.Locked));
        dto.Skipped.Should().Contain(s => s.ContestId == notSharedContest && s.Reason == nameof(PickImportSkipReason.NotShared));
        dto.TargetUsesConfidencePoints.Should().BeFalse();
    }

    [Fact]
    public async Task SetsTargetUsesConfidencePoints_WhenTargetIsConfidenceLeague()
    {
        var userId = Guid.NewGuid();
        var sourceId = SeedLeague(userId, PickType.StraightUp);
        var targetId = SeedLeague(userId, PickType.StraightUp, useConfidence: true);
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new GetPickImportPreviewQuery
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            TargetLeagueId = targetId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.TargetUsesConfidencePoints.Should().BeTrue();
    }

    [Fact]
    public async Task ExcludesDeactivatedTargetLeague()
    {
        var userId = Guid.NewGuid();
        var sourceId = SeedLeague(userId, PickType.StraightUp);
        var targetId = SeedLeague(userId, PickType.StraightUp, deactivated: true);
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new GetPickImportPreviewQuery
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            TargetLeagueId = targetId
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
