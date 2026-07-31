using FluentAssertions;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.JoinLeague;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Commands.JoinLeague;

/// <summary>
/// Join-gate behavior for league join policies. This handler serves BOTH
/// public-browse joins and invite-link joins, so these tests cover invite
/// links dying with the listing too.
/// See docs/features/league-join-policy-and-discovery.md.
/// </summary>
public class JoinLeagueJoinPolicyTests : ApiTestBase<JoinLeagueCommandHandler>
{
    private static readonly DateTime Now = new(2026, 8, 1, 18, 0, 0, DateTimeKind.Utc);

    private JoinLeagueCommandHandler CreateHandler()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
        return Mocker.CreateInstance<JoinLeagueCommandHandler>();
    }

    private async Task<Guid> SeedLeagueAsync(
        JoinPolicy policy,
        DateTime? deactivatedUtc = null,
        int? dropLowWeeks = null,
        LeagueWindow window = LeagueWindow.DateRange,
        params DateTime[] matchupStarts)
    {
        var commissionerId = Guid.NewGuid();
        var leagueId = Guid.NewGuid();

        await DataContext.Users.AddAsync(new UserEntity
        {
            Id = commissionerId,
            Username = $"user_{commissionerId:N}"[..20],
            FirebaseUid = $"uid-{commissionerId:N}",
            Email = "test@test.com",
            DisplayName = "Commish",
            SignInProvider = "test",
            LastLoginUtc = Now
        });

        await DataContext.PickemGroups.AddAsync(new PickemGroup
        {
            Id = leagueId,
            Name = "Test League",
            CommissionerUserId = commissionerId,
            Sport = Sport.BaseballMlb,
            League = League.MLB,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.TotalPoints,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            SeasonYear = 2026,
            JoinPolicy = policy,
            LeagueWindow = window,
            DropLowWeeksCount = dropLowWeeks,
            DeactivatedUtc = deactivatedUtc,
            CreatedUtc = Now,
            CreatedBy = commissionerId
        });

        var week = Guid.NewGuid();
        foreach (var start in matchupStarts)
        {
            await DataContext.PickemGroupMatchups.AddAsync(new PickemGroupMatchup
            {
                Id = Guid.NewGuid(),
                GroupId = leagueId,
                SeasonWeekId = week,
                ContestId = Guid.NewGuid(),
                StartDateUtc = start,
                SeasonYear = 2026,
                SeasonWeek = 1,
                CreatedUtc = Now,
                CreatedBy = commissionerId
            });
        }

        await DataContext.SaveChangesAsync();
        return leagueId;
    }

    private static JoinLeagueCommand Join(Guid leagueId) => new()
    {
        PickemGroupId = leagueId,
        UserId = Guid.NewGuid()
    };

    [Fact]
    public async Task OpenLeague_IsJoinable_EvenAfterGamesStart()
    {
        // Open = joinable while the league is live; started games don't close it.
        var leagueId = await SeedLeagueAsync(JoinPolicy.Open,
            matchupStarts: [Now.AddHours(-5), Now.AddHours(2)]);

        var result = await CreateHandler().ExecuteAsync(Join(leagueId));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CloseAtFirstGame_BeforeFirstGame_IsJoinable()
    {
        var leagueId = await SeedLeagueAsync(JoinPolicy.CloseAtFirstGame,
            matchupStarts: [Now.AddHours(1), Now.AddHours(4)]);

        var result = await CreateHandler().ExecuteAsync(Join(leagueId));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CloseAtFirstGame_AfterFirstGame_IsClosed()
    {
        // Second game hasn't started — the FIRST start is what closes it.
        var leagueId = await SeedLeagueAsync(JoinPolicy.CloseAtFirstGame,
            matchupStarts: [Now.AddHours(-1), Now.AddHours(3)]);

        var result = await CreateHandler().ExecuteAsync(Join(leagueId));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task CloseAtFirstGame_EmptySlate_IsJoinable()
    {
        // The slate builds asynchronously after creation. No matchups yet
        // means nothing has started — the league must be open, not closed.
        var leagueId = await SeedLeagueAsync(JoinPolicy.CloseAtFirstGame);

        var result = await CreateHandler().ExecuteAsync(Join(leagueId));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task StoredExpiry_InThePast_Rejects_EvenForOpenPolicy()
    {
        // The stored expiry (LeagueJoinExpiryCalculator's output) is the
        // authority: an Open league whose last pickable moment has passed is
        // closed, no matter what the policy enum says.
        var leagueId = await SeedLeagueAsync(JoinPolicy.Open);
        var league = await DataContext.PickemGroups.FindAsync(leagueId);
        league!.InvitationsExpireUtc = Now.AddHours(-2);
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(Join(leagueId));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task StoredExpiry_InTheFuture_Allows_EvenPastFirstGame()
    {
        // Drop-week leagues stay open past kickoff by design; the stored
        // value wins over the CloseAtFirstGame fallback derivation.
        var leagueId = await SeedLeagueAsync(JoinPolicy.CloseAtFirstGame,
            matchupStarts: [Now.AddHours(-1)]);
        var league = await DataContext.PickemGroups.FindAsync(leagueId);
        league!.InvitationsExpireUtc = Now.AddDays(10);
        await DataContext.SaveChangesAsync();

        var result = await CreateHandler().ExecuteAsync(Join(leagueId));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UncomputedExpiry_DropWeekLeague_IgnoresFirstGameFallback()
    {
        // FullSeason + drop weeks: the calculator's week-(N+1) override means
        // first-game is the WRONG close moment, so the fallback must not
        // reject while the stored expiry is still uncomputed -- even though
        // the policy says CloseAtFirstGame and the first game has started.
        var leagueId = await SeedLeagueAsync(JoinPolicy.CloseAtFirstGame,
            dropLowWeeks: 3,
            window: LeagueWindow.FullSeason,
            matchupStarts: [Now.AddHours(-1)]);

        var result = await CreateHandler().ExecuteAsync(Join(leagueId));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivatedLeague_IsNeverJoinable_RegardlessOfPolicy()
    {
        // Pre-existing bug fixed by this feature: a finished season was joinable.
        var leagueId = await SeedLeagueAsync(JoinPolicy.Open,
            deactivatedUtc: Now.AddDays(-10));

        var result = await CreateHandler().ExecuteAsync(Join(leagueId));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }
}
