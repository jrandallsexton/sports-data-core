using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.CloneLeague;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Commands.CloneLeague;

public class CloneLeagueCommandHandlerTests : ApiTestBase<CloneLeagueCommandHandler>
{
    private static readonly DateTime NowUtc = new(2026, 7, 16, 0, 0, 0, DateTimeKind.Utc);

    private CloneLeagueCommandHandler CreateHandler(Mock<IEventBus> eventBus)
    {
        var dt = new Mock<IDateTimeProvider>();
        dt.Setup(x => x.UtcNow()).Returns(NowUtc);
        return new CloneLeagueCommandHandler(
            NullLogger<CloneLeagueCommandHandler>.Instance, DataContext, eventBus.Object, dt.Object);
    }

    private Guid SeedSource(Guid ownerId, bool deactivated = false)
    {
        var id = Guid.NewGuid();
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = id,
            Name = "Source League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.AgainstTheSpread,
            UseConfidencePoints = true,
            IsPublic = true,
            CommissionerUserId = ownerId,
            DeactivatedUtc = deactivated ? NowUtc : null,
        });
        return id;
    }

    private void SeedMember(Guid groupId, Guid userId, LeagueRole role = LeagueRole.Member) =>
        DataContext.PickemGroupMembers.Add(new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            PickemGroupId = groupId,
            UserId = userId,
            Role = role,
        });

    private Guid SeedSyntheticUser()
    {
        var id = Guid.NewGuid();
        DataContext.Users.Add(new UserEntity
        {
            Id = id,
            Username = "synthetic",
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "syn@test.com",
            DisplayName = "Synthetic",
            SignInProvider = "test",
            LastLoginUtc = NowUtc,
            IsSynthetic = true,
        });
        return id;
    }

    [Fact]
    public async Task Clones_CopiesConfig_AddsOwner_AndPublishesCreated()
    {
        var userId = Guid.NewGuid();
        var sourceId = SeedSource(userId);
        SeedMember(sourceId, userId, LeagueRole.Commissioner);
        await DataContext.SaveChangesAsync();

        var eventBus = new Mock<IEventBus>();
        var result = await CreateHandler(eventBus).ExecuteAsync(new CloneLeagueCommand
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            Name = "My Clone",
        });

        result.IsSuccess.Should().BeTrue();

        var clone = DataContext.PickemGroups.AsNoTracking().Single(g => g.Id == result.Value);
        clone.Name.Should().Be("My Clone");
        clone.CommissionerUserId.Should().Be(userId);
        clone.PickType.Should().Be(PickType.AgainstTheSpread);
        clone.UseConfidencePoints.Should().BeTrue();
        clone.IsPublic.Should().BeTrue();
        clone.DeactivatedUtc.Should().BeNull();

        DataContext.PickemGroupMembers.AsNoTracking()
            .Should().Contain(m => m.PickemGroupId == clone.Id
                                   && m.UserId == userId
                                   && m.Role == LeagueRole.Commissioner);

        // Slate regenerates via the event, not a matchup copy.
        eventBus.Verify(x => x.Publish(
            It.Is<PickemGroupCreated>(e => e.GroupId == clone.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectsDeactivatedSource()
    {
        var userId = Guid.NewGuid();
        var sourceId = SeedSource(userId, deactivated: true);
        SeedMember(sourceId, userId, LeagueRole.Commissioner);
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler(new Mock<IEventBus>()).ExecuteAsync(new CloneLeagueCommand
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            Name = "My Clone",
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task RejectsNonMember()
    {
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var sourceId = SeedSource(ownerId);
        SeedMember(sourceId, ownerId, LeagueRole.Commissioner);
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler(new Mock<IEventBus>()).ExecuteAsync(new CloneLeagueCommand
        {
            UserId = strangerId, // not a member
            SourceLeagueId = sourceId,
            Name = "My Clone",
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbid);
    }

    [Fact]
    public async Task RejectsMissingName()
    {
        var userId = Guid.NewGuid();
        var sourceId = SeedSource(userId);
        SeedMember(sourceId, userId, LeagueRole.Commissioner);
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler(new Mock<IEventBus>()).ExecuteAsync(new CloneLeagueCommand
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            Name = "   ",
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task InvitesSourceMembers_ExceptSelfAndSynthetic_WhenRequested()
    {
        var userId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var syntheticId = SeedSyntheticUser();
        var sourceId = SeedSource(userId);
        SeedMember(sourceId, userId, LeagueRole.Commissioner);
        SeedMember(sourceId, friendId);
        SeedMember(sourceId, syntheticId);
        await DataContext.SaveChangesAsync();

        var eventBus = new Mock<IEventBus>();
        var result = await CreateHandler(eventBus).ExecuteAsync(new CloneLeagueCommand
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            Name = "My Clone",
            InviteMembers = true,
        });

        result.IsSuccess.Should().BeTrue();

        // Friend invited; self and synthetic are not.
        eventBus.Verify(x => x.Publish(
            It.Is<UserInvitedToPickemGroup>(e => e.InviteeUserId == friendId && e.GroupId == result.Value),
            It.IsAny<CancellationToken>()), Times.Once);
        eventBus.Verify(x => x.Publish(
            It.Is<UserInvitedToPickemGroup>(e => e.InviteeUserId == userId),
            It.IsAny<CancellationToken>()), Times.Never);
        eventBus.Verify(x => x.Publish(
            It.Is<UserInvitedToPickemGroup>(e => e.InviteeUserId == syntheticId),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DoesNotInvite_WhenNotRequested()
    {
        var userId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var sourceId = SeedSource(userId);
        SeedMember(sourceId, userId, LeagueRole.Commissioner);
        SeedMember(sourceId, friendId);
        await DataContext.SaveChangesAsync();

        var eventBus = new Mock<IEventBus>();
        var result = await CreateHandler(eventBus).ExecuteAsync(new CloneLeagueCommand
        {
            UserId = userId,
            SourceLeagueId = sourceId,
            Name = "My Clone",
            InviteMembers = false,
        });

        result.IsSuccess.Should().BeTrue();
        eventBus.Verify(x => x.Publish(
            It.IsAny<UserInvitedToPickemGroup>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
