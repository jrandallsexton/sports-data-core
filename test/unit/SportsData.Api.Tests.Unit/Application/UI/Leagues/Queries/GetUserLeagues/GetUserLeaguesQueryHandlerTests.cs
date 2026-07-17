using FluentAssertions;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Queries.GetUserLeagues;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Queries.GetUserLeagues;

public class GetUserLeaguesQueryHandlerTests : ApiTestBase<GetUserLeaguesQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_WhenIncludeDeactivated_ShouldReturnPastLeaguesMarkedDeactivated()
    {
        // Arrange — same two memberships as the exclusion test, but the caller
        // opts in. Past leagues must come back AND carry DeactivatedUtc, since
        // that non-null value is what tells the UI to hide Duplicate.
        var userId = Guid.NewGuid();
        var deactivatedOn = DateTime.UtcNow.AddDays(-7);

        var user = new UserEntity
        {
            Username = "test_user_7",
            Id = userId,
            FirebaseUid = "firebase-past-leagues",
            Email = "past@example.com",
            DisplayName = "P",
            SignInProvider = "password",
            EmailVerified = true,
            CreatedUtc = DateTime.UtcNow.AddDays(-30),
            LastLoginUtc = DateTime.UtcNow
        };

        var activeGroup = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Active League",
            Sport = Sport.BaseballMlb,
            League = League.MLB
        };

        var deactivatedGroup = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Last Season",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            DeactivatedUtc = deactivatedOn
        };

        var activeMembership = new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = activeGroup.Id,
            User = user,
            Group = activeGroup
        };

        var deactivatedMembership = new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = deactivatedGroup.Id,
            User = user,
            Group = deactivatedGroup
        };

        user.GroupMemberships.Add(activeMembership);
        user.GroupMemberships.Add(deactivatedMembership);

        await DataContext.Users.AddAsync(user);
        await DataContext.PickemGroups.AddRangeAsync(activeGroup, deactivatedGroup);
        await DataContext.PickemGroupMembers.AddRangeAsync(activeMembership, deactivatedMembership);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetUserLeaguesQueryHandler>();
        var query = new GetUserLeaguesQuery { UserId = userId, IncludeDeactivated = true };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var active = result.Value.Single(l => l.Id == activeGroup.Id);
        active.DeactivatedUtc.Should().BeNull();
        active.League.Should().Be(nameof(League.MLB));

        var past = result.Value.Single(l => l.Id == deactivatedGroup.Id);
        past.DeactivatedUtc.Should().Be(deactivatedOn);
        past.League.Should().Be(nameof(League.NCAAF));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExcludeDeactivatedLeagues()
    {
        // Arrange — one active and one deactivated membership for the same user.
        // Regression guard: without the DeactivatedUtc filter the handler would
        // return both rows, leaking stale prior-season leagues into the UI.
        var userId = Guid.NewGuid();

        var user = new UserEntity
        {
            Username = "test_user_6",
            Id = userId,
            FirebaseUid = "firebase-dead-league",
            Email = "u@example.com",
            DisplayName = "U",
            SignInProvider = "password",
            EmailVerified = true,
            CreatedUtc = DateTime.UtcNow.AddDays(-30),
            LastLoginUtc = DateTime.UtcNow
        };

        var activeGroup = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Active League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF
        };

        var deactivatedGroup = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Last Season",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            DeactivatedUtc = DateTime.UtcNow.AddDays(-7)
        };

        var activeMembership = new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = activeGroup.Id,
            User = user,
            Group = activeGroup
        };

        var deactivatedMembership = new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PickemGroupId = deactivatedGroup.Id,
            User = user,
            Group = deactivatedGroup
        };

        user.GroupMemberships.Add(activeMembership);
        user.GroupMemberships.Add(deactivatedMembership);

        await DataContext.Users.AddAsync(user);
        await DataContext.PickemGroups.AddRangeAsync(activeGroup, deactivatedGroup);
        await DataContext.PickemGroupMembers.AddRangeAsync(activeMembership, deactivatedMembership);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetUserLeaguesQueryHandler>();
        var query = new GetUserLeaguesQuery { UserId = userId };

        // Act
        var result = await handler.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
        result.Value.Single().Id.Should().Be(activeGroup.Id);
        result.Value.Single().Name.Should().Be("Active League");
    }
}
