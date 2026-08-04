using FluentAssertions;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.AcceptLeagueInvitation;
using SportsData.Api.Application.UI.Leagues.Commands.DeclineLeagueInvitation;
using SportsData.Api.Application.UI.Leagues.Commands.JoinLeague;
using SportsData.Api.Application.UI.Leagues.Queries.GetPendingInvitations;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Commands.LeagueInvitations;

/// <summary>
/// Pending-invitation lifecycle: the GetPendingInvitations query that powers
/// the home cards, and the accept/decline commands. Accept delegates to the
/// REAL JoinLeagueCommandHandler (not a mock) so join-policy gates are
/// exercised end-to-end — an invitation must not bypass league rules.
/// </summary>
public class LeagueInvitationTests : ApiTestBase<AcceptLeagueInvitationCommandHandler>
{
    private static readonly DateTime Now = new(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

    public LeagueInvitationTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
        // Accept delegates to the real join handler sharing the same DataContext.
        Mocker.Use<IJoinLeagueCommandHandler>(Mocker.CreateInstance<JoinLeagueCommandHandler>());
    }

    private AcceptLeagueInvitationCommandHandler CreateAcceptHandler()
        => Mocker.CreateInstance<AcceptLeagueInvitationCommandHandler>();

    private DeclineLeagueInvitationCommandHandler CreateDeclineHandler()
        => Mocker.CreateInstance<DeclineLeagueInvitationCommandHandler>();

    private GetPendingInvitationsQueryHandler CreateQueryHandler()
        => Mocker.CreateInstance<GetPendingInvitationsQueryHandler>();

    private async Task<Guid> SeedUserAsync(string name = "User")
    {
        var id = Guid.NewGuid();
        await DataContext.Users.AddAsync(new UserEntity
        {
            Id = id,
            Username = $"user_{id:N}"[..20],
            FirebaseUid = $"uid-{id:N}",
            Email = $"{id:N}@test.com",
            DisplayName = name,
            SignInProvider = "test",
            LastLoginUtc = Now
        });
        await DataContext.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> SeedLeagueAsync(
        Guid commissionerId,
        DateTime? deactivatedUtc = null,
        DateTime? invitationsExpireUtc = null,
        JoinPolicy policy = JoinPolicy.Open)
    {
        var leagueId = Guid.NewGuid();
        await DataContext.PickemGroups.AddAsync(new PickemGroup
        {
            Id = leagueId,
            Name = "Invite League",
            CommissionerUserId = commissionerId,
            Sport = Sport.BaseballMlb,
            League = League.MLB,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            SeasonYear = 2026,
            JoinPolicy = policy,
            DeactivatedUtc = deactivatedUtc,
            InvitationsExpireUtc = invitationsExpireUtc,
            CreatedUtc = Now,
            CreatedBy = commissionerId
        });
        await DataContext.SaveChangesAsync();
        return leagueId;
    }

    private async Task<Guid> SeedInvitationAsync(
        Guid leagueId,
        Guid invitedById,
        Guid inviteeId,
        DateTime? acceptedUtc = null,
        DateTime? declinedUtc = null,
        bool isRevoked = false)
    {
        var id = Guid.NewGuid();
        await DataContext.PickemGroupInvitations.AddAsync(new PickemGroupInvitation
        {
            Id = id,
            PickemGroupId = leagueId,
            InvitedByUserId = invitedById,
            InviteeUserId = inviteeId,
            AcceptedUtc = acceptedUtc,
            DeclinedUtc = declinedUtc,
            IsRevoked = isRevoked,
            CreatedUtc = Now.AddHours(-1),
            CreatedBy = invitedById
        });
        await DataContext.SaveChangesAsync();
        return id;
    }

    // ── Accept ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Accept_PendingInvitation_JoinsLeagueAndStamps()
    {
        var inviter = await SeedUserAsync("Commish");
        var invitee = await SeedUserAsync("Invitee");
        var leagueId = await SeedLeagueAsync(inviter);
        var invitationId = await SeedInvitationAsync(leagueId, inviter, invitee);

        var result = await CreateAcceptHandler().ExecuteAsync(invitationId, invitee);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(leagueId);
        DataContext.PickemGroupMembers
            .Any(m => m.PickemGroupId == leagueId && m.UserId == invitee)
            .Should().BeTrue();
        DataContext.PickemGroupInvitations.Single(i => i.Id == invitationId)
            .AcceptedUtc.Should().Be(Now);
    }

    [Fact]
    public async Task Accept_SomeoneElsesInvitation_IsNotFound()
    {
        var inviter = await SeedUserAsync();
        var invitee = await SeedUserAsync();
        var interloper = await SeedUserAsync();
        var leagueId = await SeedLeagueAsync(inviter);
        var invitationId = await SeedInvitationAsync(leagueId, inviter, invitee);

        var result = await CreateAcceptHandler().ExecuteAsync(invitationId, interloper);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        DataContext.PickemGroupMembers.Any(m => m.UserId == interloper).Should().BeFalse();
    }

    [Fact]
    public async Task Accept_DeclinedInvitation_Fails()
    {
        var inviter = await SeedUserAsync();
        var invitee = await SeedUserAsync();
        var leagueId = await SeedLeagueAsync(inviter);
        var invitationId = await SeedInvitationAsync(
            leagueId, inviter, invitee, declinedUtc: Now.AddMinutes(-5));

        var result = await CreateAcceptHandler().ExecuteAsync(invitationId, invitee);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task Accept_ClosedLeague_PropagatesJoinFailure_AndStaysPending()
    {
        var inviter = await SeedUserAsync();
        var invitee = await SeedUserAsync();
        // Expired league — the join gate rejects, and the invitation must NOT
        // be stamped accepted.
        var leagueId = await SeedLeagueAsync(
            inviter, invitationsExpireUtc: Now.AddDays(-1));
        var invitationId = await SeedInvitationAsync(leagueId, inviter, invitee);

        var result = await CreateAcceptHandler().ExecuteAsync(invitationId, invitee);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        DataContext.PickemGroupInvitations.Single(i => i.Id == invitationId)
            .AcceptedUtc.Should().BeNull();
        DataContext.PickemGroupMembers.Any(m => m.UserId == invitee).Should().BeFalse();
    }

    [Fact]
    public async Task Accept_WhenAlreadyMemberViaOtherPath_SelfHealsAndSucceeds()
    {
        var inviter = await SeedUserAsync();
        var invitee = await SeedUserAsync();
        var leagueId = await SeedLeagueAsync(inviter);
        var invitationId = await SeedInvitationAsync(leagueId, inviter, invitee);

        // User joined via public browse before touching the invitation.
        await DataContext.PickemGroupMembers.AddAsync(new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = invitee,
            Role = LeagueRole.Member,
            CreatedUtc = Now,
            CreatedBy = invitee
        });
        await DataContext.SaveChangesAsync();

        var result = await CreateAcceptHandler().ExecuteAsync(invitationId, invitee);

        result.IsSuccess.Should().BeTrue();
        DataContext.PickemGroupInvitations.Single(i => i.Id == invitationId)
            .AcceptedUtc.Should().Be(Now);
    }

    // ── Decline ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Decline_PendingInvitation_Stamps()
    {
        var inviter = await SeedUserAsync();
        var invitee = await SeedUserAsync();
        var leagueId = await SeedLeagueAsync(inviter);
        var invitationId = await SeedInvitationAsync(leagueId, inviter, invitee);

        var result = await CreateDeclineHandler().ExecuteAsync(invitationId, invitee);

        result.IsSuccess.Should().BeTrue();
        DataContext.PickemGroupInvitations.Single(i => i.Id == invitationId)
            .DeclinedUtc.Should().Be(Now);
        DataContext.PickemGroupMembers.Any(m => m.UserId == invitee).Should().BeFalse();
    }

    [Fact]
    public async Task Decline_SomeoneElsesInvitation_IsNotFound()
    {
        var inviter = await SeedUserAsync();
        var invitee = await SeedUserAsync();
        var interloper = await SeedUserAsync();
        var leagueId = await SeedLeagueAsync(inviter);
        var invitationId = await SeedInvitationAsync(leagueId, inviter, invitee);

        var result = await CreateDeclineHandler().ExecuteAsync(invitationId, interloper);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        DataContext.PickemGroupInvitations.Single(i => i.Id == invitationId)
            .DeclinedUtc.Should().BeNull();
    }

    // ── Pending query ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPending_ReturnsOnlyActionableInvitations()
    {
        var inviter = await SeedUserAsync("Commish");
        var invitee = await SeedUserAsync("Invitee");

        var openLeague = await SeedLeagueAsync(inviter);
        var deadLeague = await SeedLeagueAsync(inviter, deactivatedUtc: Now.AddDays(-1));
        var expiredLeague = await SeedLeagueAsync(inviter, invitationsExpireUtc: Now.AddDays(-1));
        var joinedLeague = await SeedLeagueAsync(inviter);

        var pendingId = await SeedInvitationAsync(openLeague, inviter, invitee);
        await SeedInvitationAsync(deadLeague, inviter, invitee);          // league deactivated
        await SeedInvitationAsync(expiredLeague, inviter, invitee);       // league expired
        await SeedInvitationAsync(joinedLeague, inviter, invitee);        // already a member
        await SeedInvitationAsync(openLeague, inviter, invitee, acceptedUtc: Now);  // accepted
        await SeedInvitationAsync(openLeague, inviter, invitee, declinedUtc: Now);  // declined
        await SeedInvitationAsync(openLeague, inviter, invitee, isRevoked: true);   // revoked

        await DataContext.PickemGroupMembers.AddAsync(new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            PickemGroupId = joinedLeague,
            UserId = invitee,
            Role = LeagueRole.Member,
            CreatedUtc = Now,
            CreatedBy = invitee
        });
        await DataContext.SaveChangesAsync();

        var result = await CreateQueryHandler().ExecuteAsync(invitee);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        var dto = result.Value.Single();
        dto.InvitationId.Should().Be(pendingId);
        dto.InvitedBy.Should().Be("Commish");
        // Embedded league parameters power the join-confirmation dialog.
        dto.League.Id.Should().Be(openLeague);
        dto.League.Name.Should().Be("Invite League");
        dto.League.Sport.Should().Be(Sport.BaseballMlb);
        dto.League.Commissioner.Should().Be("Commish");
        dto.League.MemberCount.Should().Be(0);
        dto.League.IsJoinable.Should().BeTrue();
    }

    [Fact]
    public async Task GetPending_OtherUsersInvitations_AreInvisible()
    {
        var inviter = await SeedUserAsync();
        var invitee = await SeedUserAsync();
        var stranger = await SeedUserAsync();
        var leagueId = await SeedLeagueAsync(inviter);
        await SeedInvitationAsync(leagueId, inviter, invitee);

        var result = await CreateQueryHandler().ExecuteAsync(stranger);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
