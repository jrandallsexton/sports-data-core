using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Leagues.Commands.InviteUserToLeague;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Commands.InviteUserToLeague;

public class InviteUserToLeagueCommandHandlerTests : ApiTestBase<InviteUserToLeagueCommandHandler>
{
    private async Task<PickemGroup> SeedLeagueAsync()
    {
        var league = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Saturday Smackdown",
            Sport = Sport.FootballNcaa,
            League = SportsData.Api.Application.Common.Enums.League.NCAAF
        };
        await DataContext.PickemGroups.AddAsync(league);
        await DataContext.SaveChangesAsync();
        return league;
    }

    private async Task<Guid> SeedUserAsync()
    {
        var id = Guid.NewGuid();
        await DataContext.Users.AddAsync(new UserEntity
        {
            Id = id,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "invitee@x.com",
            SignInProvider = "password",
            DisplayName = "Invitee",
            Username = "invitee"
        });
        await DataContext.SaveChangesAsync();
        return id;
    }

    private async Task AddMemberAsync(Guid leagueId, Guid userId)
    {
        await DataContext.PickemGroupMembers.AddAsync(new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId
        });
        await DataContext.SaveChangesAsync();
    }

    private void VerifyPublish(Times times) =>
        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(It.IsAny<UserInvitedToPickemGroup>(), It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task Execute_PublishesInvite_ForRegisteredNonMember()
    {
        var league = await SeedLeagueAsync();
        var inviteeId = await SeedUserAsync();
        var invitedBy = Guid.NewGuid();
        await AddMemberAsync(league.Id, invitedBy);
        var handler = Mocker.CreateInstance<InviteUserToLeagueCommandHandler>();

        var result = await handler.ExecuteAsync(new InviteUserToLeagueCommand
        {
            LeagueId = league.Id,
            InviteeUserId = inviteeId,
            InvitedByUserId = invitedBy
        });

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(
                It.Is<UserInvitedToPickemGroup>(e =>
                    e.InviteeUserId == inviteeId &&
                    e.GroupId == league.Id &&
                    e.LeagueName == league.Name &&
                    e.InvitedByUserId == invitedBy &&
                    e.Sport == league.Sport),
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task Execute_NotFound_WhenLeagueMissing()
    {
        var inviteeId = await SeedUserAsync();
        var handler = Mocker.CreateInstance<InviteUserToLeagueCommandHandler>();

        var result = await handler.ExecuteAsync(new InviteUserToLeagueCommand
        {
            LeagueId = Guid.NewGuid(),
            InviteeUserId = inviteeId,
            InvitedByUserId = Guid.NewGuid()
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        VerifyPublish(Times.Never());
    }

    [Fact]
    public async Task Execute_NotFound_WhenUserMissing()
    {
        var league = await SeedLeagueAsync();
        var invitedBy = Guid.NewGuid();
        await AddMemberAsync(league.Id, invitedBy);
        var handler = Mocker.CreateInstance<InviteUserToLeagueCommandHandler>();

        var result = await handler.ExecuteAsync(new InviteUserToLeagueCommand
        {
            LeagueId = league.Id,
            InviteeUserId = Guid.NewGuid(),
            InvitedByUserId = invitedBy
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        VerifyPublish(Times.Never());
    }

    [Fact]
    public async Task Execute_Rejects_WhenAlreadyMember()
    {
        var league = await SeedLeagueAsync();
        var inviteeId = await SeedUserAsync();
        await AddMemberAsync(league.Id, inviteeId);
        var invitedBy = Guid.NewGuid();
        await AddMemberAsync(league.Id, invitedBy);

        var handler = Mocker.CreateInstance<InviteUserToLeagueCommandHandler>();

        var result = await handler.ExecuteAsync(new InviteUserToLeagueCommand
        {
            LeagueId = league.Id,
            InviteeUserId = inviteeId,
            InvitedByUserId = invitedBy
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        VerifyPublish(Times.Never());
    }

    [Fact]
    public async Task Execute_Forbids_WhenInviterNotAMember()
    {
        var league = await SeedLeagueAsync();
        var inviteeId = await SeedUserAsync();
        var handler = Mocker.CreateInstance<InviteUserToLeagueCommandHandler>();

        var result = await handler.ExecuteAsync(new InviteUserToLeagueCommand
        {
            LeagueId = league.Id,
            InviteeUserId = inviteeId,
            InvitedByUserId = Guid.NewGuid() // not a member of the league
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbid);
        VerifyPublish(Times.Never());
    }
}
