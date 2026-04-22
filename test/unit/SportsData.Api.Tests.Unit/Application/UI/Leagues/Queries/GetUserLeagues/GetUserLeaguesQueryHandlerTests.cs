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
    public async Task ExecuteAsync_ShouldExcludeDeactivatedLeagues()
    {
        // Arrange — one active and one deactivated membership for the same user.
        // Regression guard: without the DeactivatedUtc filter the handler would
        // return both rows, leaking stale prior-season leagues into the UI.
        var userId = Guid.NewGuid();

        var user = new UserEntity
        {
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
