using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboard;
using SportsData.Api.Application.UI.Leagues.Authorization;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueById;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueScoresByWeek;
using SportsData.Api.Application.UI.Picks.Queries.GetUserPicksByGroupAndWeek;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Authorization;

/// <summary>
/// Authorization behavior for by-group reads — the IDOR closure.
/// Possession of a league GUID must not grant access; see
/// docs/audit/league-authorization-idor.md.
///
/// These tests drive the REAL <see cref="LeagueMembershipGuard"/> against the
/// in-memory context rather than the permissive mock the base class installs,
/// so membership is decided by seeded rows exactly as it is in production.
/// </summary>
public class LeagueAuthorizationTests : ApiTestBase<LeagueMembershipGuard>
{
    private static readonly DateTime FixedNow = new(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);

    private static UserEntity NewUser(Guid id) => new()
    {
        Id = id,
        Username = $"user_{id:N}"[..20],
        FirebaseUid = $"uid-{id:N}",
        Email = "test@test.com",
        DisplayName = $"User {id:N}"[..12],
        SignInProvider = "test",
        LastLoginUtc = FixedNow
    };

    /// <summary>Seeds a league whose sole member is <paramref name="memberId"/>.</summary>
    private async Task<Guid> SeedLeagueAsync(Guid memberId, bool isPublic = false)
    {
        var leagueId = Guid.NewGuid();
        await DataContext.Users.AddAsync(NewUser(memberId));
        await DataContext.PickemGroups.AddAsync(new PickemGroup
        {
            Id = leagueId,
            Name = "Test League",
            CommissionerUserId = memberId,
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            SeasonYear = 2026,
            IsPublic = isPublic,
            CreatedUtc = FixedNow,
            CreatedBy = memberId
        });
        await DataContext.PickemGroupMembers.AddAsync(new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = memberId,
            Role = LeagueRole.Commissioner,
            CreatedUtc = FixedNow,
            CreatedBy = memberId
        });
        await DataContext.SaveChangesAsync();
        return leagueId;
    }

    // ── The guard itself ──────────────────────────────────────────────────────

    [Fact]
    public async Task Guard_TrueForMember_FalseForStranger()
    {
        var memberId = Guid.NewGuid();
        var leagueId = await SeedLeagueAsync(memberId);
        var guard = new LeagueMembershipGuard(DataContext);

        (await guard.IsMemberAsync(leagueId, memberId)).Should().BeTrue();
        (await guard.IsMemberAsync(leagueId, Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Guard_FalseForEmptyIds_AndUnknownLeague()
    {
        var memberId = Guid.NewGuid();
        var leagueId = await SeedLeagueAsync(memberId);
        var guard = new LeagueMembershipGuard(DataContext);

        (await guard.IsMemberAsync(Guid.Empty, memberId)).Should().BeFalse();
        (await guard.IsMemberAsync(leagueId, Guid.Empty)).Should().BeFalse();
        (await guard.IsMemberAsync(Guid.NewGuid(), memberId)).Should().BeFalse("an unknown league grants nothing");
    }

    /// <summary>Seeds a thread with one post in <paramref name="leagueId"/>.</summary>
    private async Task<(Guid ThreadId, Guid PostId)> SeedThreadWithPostAsync(Guid leagueId, Guid authorId)
    {
        var threadId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        await DataContext.AddAsync(new MessageThread
        {
            Id = threadId,
            GroupId = leagueId,
            Title = "Trash talk",
            Slug = "trash-talk",
            LastActivityAt = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = authorId
        });
        await DataContext.AddAsync(new MessagePost
        {
            Id = postId,
            ThreadId = threadId,
            Content = "Roll Tide",
            Path = "0001",
            CreatedUtc = FixedNow,
            CreatedBy = authorId
        });
        await DataContext.SaveChangesAsync();
        return (threadId, postId);
    }

    [Fact]
    public async Task Guard_ThreadAndPostVariants_ResolveTheOwningLeague()
    {
        var memberId = Guid.NewGuid();
        var leagueId = await SeedLeagueAsync(memberId);
        var (threadId, postId) = await SeedThreadWithPostAsync(leagueId, memberId);
        var guard = new LeagueMembershipGuard(DataContext);

        // The member of the league that owns the thread/post gets through...
        (await guard.IsMemberOfThreadGroupAsync(threadId, memberId)).Should().BeTrue();
        (await guard.IsMemberOfPostGroupAsync(postId, memberId)).Should().BeTrue();

        // ...and a stranger does not, even holding a real thread/post id.
        var strangerId = Guid.NewGuid();
        (await guard.IsMemberOfThreadGroupAsync(threadId, strangerId)).Should().BeFalse();
        (await guard.IsMemberOfPostGroupAsync(postId, strangerId)).Should().BeFalse();
    }

    [Fact]
    public async Task Guard_ThreadAndPostVariants_FalseForUnknownIds()
    {
        var memberId = Guid.NewGuid();
        var leagueId = await SeedLeagueAsync(memberId);
        await SeedThreadWithPostAsync(leagueId, memberId);
        var guard = new LeagueMembershipGuard(DataContext);

        // A bogus thread/post id must be denied, not error — even for a user
        // who legitimately belongs to a league.
        (await guard.IsMemberOfThreadGroupAsync(Guid.NewGuid(), memberId)).Should().BeFalse();
        (await guard.IsMemberOfPostGroupAsync(Guid.NewGuid(), memberId)).Should().BeFalse();
        (await guard.IsMemberOfThreadGroupAsync(Guid.Empty, memberId)).Should().BeFalse();
        (await guard.IsMemberOfPostGroupAsync(Guid.Empty, memberId)).Should().BeFalse();
    }

    // ── Tiered league detail ──────────────────────────────────────────────────

    [Fact]
    public async Task GetLeagueById_Member_ReceivesRoster()
    {
        var memberId = Guid.NewGuid();
        var leagueId = await SeedLeagueAsync(memberId);
        var handler = new GetLeagueByIdQueryHandler(DataContext);

        var result = await handler.ExecuteAsync(new GetLeagueByIdQuery
        {
            LeagueId = leagueId,
            UserId = memberId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.IsMember.Should().BeTrue();
        result.Value.Members.Should().HaveCount(1);
        result.Value.MemberCount.Should().Be(1);
    }

    [Fact]
    public async Task GetLeagueById_NonMember_RosterWithheld_CountPreserved()
    {
        // The invite-preview and public-browse case: a non-member may see WHAT
        // the league is and how big it is, but not WHO is in it.
        var memberId = Guid.NewGuid();
        var leagueId = await SeedLeagueAsync(memberId);
        var handler = new GetLeagueByIdQueryHandler(DataContext);

        var result = await handler.ExecuteAsync(new GetLeagueByIdQuery
        {
            LeagueId = leagueId,
            UserId = Guid.NewGuid()
        });

        result.IsSuccess.Should().BeTrue("the invite preview depends on this succeeding");
        result.Value.IsMember.Should().BeFalse();
        result.Value.Members.Should().BeEmpty("the roster is the privacy payload");
        result.Value.MemberCount.Should().Be(1, "clients render 'N members' from this");
        result.Value.Name.Should().Be("Test League");
    }

    // ── Guarded reads ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLeaderboard_NonMember_IsForbidden()
    {
        var memberId = Guid.NewGuid();
        var leagueId = await SeedLeagueAsync(memberId);
        DenyLeagueMembership();
        var handler = Mocker.CreateInstance<GetLeaderboardQueryHandler>();

        var result = await handler.ExecuteAsync(new GetLeaderboardQuery
        {
            GroupId = leagueId,
            UserId = Guid.NewGuid()
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbid);
    }

    [Fact]
    public async Task GetLeagueScoresByWeek_NonMember_IsForbidden()
    {
        var memberId = Guid.NewGuid();
        var leagueId = await SeedLeagueAsync(memberId);
        DenyLeagueMembership();
        var handler = Mocker.CreateInstance<GetLeagueScoresByWeekQueryHandler>();

        var result = await handler.ExecuteAsync(new GetLeagueScoresByWeekQuery
        {
            LeagueId = leagueId,
            UserId = Guid.NewGuid()
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbid);
    }

    [Fact]
    public async Task GetUserPicksByGroupAndWeek_NonMember_IsForbidden()
    {
        var memberId = Guid.NewGuid();
        var leagueId = await SeedLeagueAsync(memberId);
        DenyLeagueMembership();
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);
        var handler = Mocker.CreateInstance<GetUserPicksByGroupAndWeekQueryHandler>();

        var result = await handler.ExecuteAsync(new GetUserPicksByGroupAndWeekQuery
        {
            GroupId = leagueId,
            UserId = Guid.NewGuid(),
            WeekNumber = 1
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbid);
    }

    [Fact]
    public async Task GuardedReads_StillSucceedForMembers()
    {
        // Guards must not break the happy path — the permissive default from
        // ApiTestBase stands in for a real membership row here.
        var memberId = Guid.NewGuid();
        var leagueId = await SeedLeagueAsync(memberId);
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);

        var picks = await Mocker.CreateInstance<GetUserPicksByGroupAndWeekQueryHandler>()
            .ExecuteAsync(new GetUserPicksByGroupAndWeekQuery
            {
                GroupId = leagueId,
                UserId = memberId,
                WeekNumber = 1
            });

        picks.IsSuccess.Should().BeTrue();
    }
}
