using FluentAssertions;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.PickImport.Queries.GetPickImportSources;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.PickImport;

public class GetPickImportSourcesQueryHandlerTests : ApiTestBase<GetPickImportSourcesQueryHandler>
{
    private GetPickImportSourcesQueryHandler CreateHandler() => new(DataContext);

    private Guid SeedLeague(Guid userId, PickType pickType, bool member = true, bool deactivated = false, string name = "League")
    {
        var groupId = Guid.NewGuid();
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = groupId,
            Name = name,
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = pickType,
            DeactivatedUtc = deactivated ? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) : null,
            CommissionerUserId = userId
        });
        if (member)
        {
            DataContext.PickemGroupMembers.Add(new PickemGroupMember
            {
                Id = Guid.NewGuid(),
                PickemGroupId = groupId,
                UserId = userId,
                Role = LeagueRole.Member
            });
        }
        return groupId;
    }

    private void SeedMatchup(Guid groupId, Guid contestId)
    {
        DataContext.PickemGroupMatchups.Add(new PickemGroupMatchup
        {
            GroupId = groupId,
            SeasonWeekId = Guid.NewGuid(),
            ContestId = contestId,
            StartDateUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonYear = 2026,
            SeasonWeek = 1,
            Headline = "Away @ Home"
        });
    }

    [Fact]
    public async Task ReturnsNotFound_WhenNotMemberOfTarget()
    {
        var result = await CreateHandler().ExecuteAsync(new GetPickImportSourcesQuery
        {
            UserId = Guid.NewGuid(),
            TargetLeagueId = Guid.NewGuid()
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ReturnsSameTypeSourcesSharingContests_WithSharedCount()
    {
        var userId = Guid.NewGuid();
        var sharedA = Guid.NewGuid();
        var sharedB = Guid.NewGuid();

        var targetId = SeedLeague(userId, PickType.StraightUp, name: "Target");
        SeedMatchup(targetId, sharedA);
        SeedMatchup(targetId, sharedB);

        // Same-type source sharing both contests.
        var goodSource = SeedLeague(userId, PickType.StraightUp, name: "Good");
        SeedMatchup(goodSource, sharedA);
        SeedMatchup(goodSource, sharedB);
        SeedMatchup(goodSource, Guid.NewGuid()); // extra, non-shared

        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new GetPickImportSourcesQuery
        {
            UserId = userId,
            TargetLeagueId = targetId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].LeagueId.Should().Be(goodSource);
        result.Value[0].SharedContestCount.Should().Be(2);
    }

    [Fact]
    public async Task ExcludesDifferentPickTypeSources()
    {
        var userId = Guid.NewGuid();
        var shared = Guid.NewGuid();

        var targetId = SeedLeague(userId, PickType.StraightUp, name: "Target");
        SeedMatchup(targetId, shared);

        var atsSource = SeedLeague(userId, PickType.AgainstTheSpread, name: "ATS");
        SeedMatchup(atsSource, shared);

        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new GetPickImportSourcesQuery
        {
            UserId = userId,
            TargetLeagueId = targetId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExcludesSameTypeSourcesWithNoSharedContest()
    {
        var userId = Guid.NewGuid();

        var targetId = SeedLeague(userId, PickType.StraightUp, name: "Target");
        SeedMatchup(targetId, Guid.NewGuid());

        var disjointSource = SeedLeague(userId, PickType.StraightUp, name: "Disjoint");
        SeedMatchup(disjointSource, Guid.NewGuid()); // different contest

        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new GetPickImportSourcesQuery
        {
            UserId = userId,
            TargetLeagueId = targetId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExcludesLeaguesTheUserIsNotAMemberOf()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var shared = Guid.NewGuid();

        var targetId = SeedLeague(userId, PickType.StraightUp, name: "Target");
        SeedMatchup(targetId, shared);

        // Same-type league sharing a contest, but the user is NOT a member.
        var foreignSource = SeedLeague(otherUserId, PickType.StraightUp, member: false, name: "Foreign");
        DataContext.PickemGroupMembers.Add(new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            PickemGroupId = foreignSource,
            UserId = otherUserId,
            Role = LeagueRole.Member
        });
        SeedMatchup(foreignSource, shared);

        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new GetPickImportSourcesQuery
        {
            UserId = userId,
            TargetLeagueId = targetId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExcludesDeactivatedSourceLeagues()
    {
        var userId = Guid.NewGuid();
        var shared = Guid.NewGuid();

        var targetId = SeedLeague(userId, PickType.StraightUp, name: "Target");
        SeedMatchup(targetId, shared);

        var deadSource = SeedLeague(userId, PickType.StraightUp, deactivated: true, name: "Dead");
        SeedMatchup(deadSource, shared);

        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(new GetPickImportSourcesQuery
        {
            UserId = userId,
            TargetLeagueId = targetId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
