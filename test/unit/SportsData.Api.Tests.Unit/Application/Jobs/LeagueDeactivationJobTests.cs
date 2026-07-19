using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Jobs;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Jobs;

/// <summary>
/// Tests for <see cref="LeagueDeactivationJob"/>. Rule 1: deactivate leagues
/// whose EndsOn is more than 7 days past. Validates the grace boundary, the
/// null-EndsOn (season-long) exclusion, and idempotency against already-
/// deactivated leagues.
/// </summary>
public class LeagueDeactivationJobTests : ApiTestBase<LeagueDeactivationJob>
{
    // Anchor "now". Grace window is 7 days, so the cutoff is 2026-07-12 12:00Z:
    // a league is deactivated iff EndsOn <= cutoff.
    private static readonly DateTime FixedNow = new(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Cutoff = FixedNow.AddDays(-7);

    public LeagueDeactivationJobTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);
    }

    [Fact]
    public async Task ExecuteAsync_DeactivatesLeague_PastGraceWindow()
    {
        var leagueId = Guid.NewGuid();
        await SeedLeagueAsync(leagueId, endsOn: FixedNow.AddDays(-9));

        var sut = Mocker.CreateInstance<LeagueDeactivationJob>();
        await sut.ExecuteAsync();

        var group = await DataContext.PickemGroups.AsNoTracking().SingleAsync(g => g.Id == leagueId);
        Assert.Equal(FixedNow, group.DeactivatedUtc);
    }

    [Fact]
    public async Task ExecuteAsync_DeactivatesLeague_ExactlyAtGraceBoundary()
    {
        // EndsOn == cutoff (exactly 7 days past). Predicate is EndsOn <= cutoff,
        // so the boundary deactivates.
        var leagueId = Guid.NewGuid();
        await SeedLeagueAsync(leagueId, endsOn: Cutoff);

        var sut = Mocker.CreateInstance<LeagueDeactivationJob>();
        await sut.ExecuteAsync();

        var group = await DataContext.PickemGroups.AsNoTracking().SingleAsync(g => g.Id == leagueId);
        Assert.Equal(FixedNow, group.DeactivatedUtc);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotDeactivate_WithinGraceWindow()
    {
        // Ended 4 days ago — still inside the 7-day grace window.
        var leagueId = Guid.NewGuid();
        await SeedLeagueAsync(leagueId, endsOn: FixedNow.AddDays(-4));

        var sut = Mocker.CreateInstance<LeagueDeactivationJob>();
        await sut.ExecuteAsync();

        var group = await DataContext.PickemGroups.AsNoTracking().SingleAsync(g => g.Id == leagueId);
        Assert.Null(group.DeactivatedUtc);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotDeactivate_NullEndsOn()
    {
        // Season-long league (no explicit end) is out of scope for rule 1.
        var leagueId = Guid.NewGuid();
        await SeedLeagueAsync(leagueId, endsOn: null);

        var sut = Mocker.CreateInstance<LeagueDeactivationJob>();
        await sut.ExecuteAsync();

        var group = await DataContext.PickemGroups.AsNoTracking().SingleAsync(g => g.Id == leagueId);
        Assert.Null(group.DeactivatedUtc);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRestamp_AlreadyDeactivatedLeague()
    {
        // Already closed (manually or a prior run). EndsOn is well past, but the
        // job must not overwrite the original DeactivatedUtc.
        var leagueId = Guid.NewGuid();
        var originalDeactivatedUtc = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        await SeedLeagueAsync(leagueId, endsOn: FixedNow.AddDays(-30), deactivatedUtc: originalDeactivatedUtc);

        var sut = Mocker.CreateInstance<LeagueDeactivationJob>();
        await sut.ExecuteAsync();

        var group = await DataContext.PickemGroups.AsNoTracking().SingleAsync(g => g.Id == leagueId);
        Assert.Equal(originalDeactivatedUtc, group.DeactivatedUtc);
    }

    [Fact]
    public async Task ExecuteAsync_OnlyDeactivatesEligibleLeagues_InMixedSet()
    {
        var eligibleId = Guid.NewGuid();
        var withinWindowId = Guid.NewGuid();
        var nullEndsOnId = Guid.NewGuid();

        await SeedLeagueAsync(eligibleId, endsOn: FixedNow.AddDays(-10));
        await SeedLeagueAsync(withinWindowId, endsOn: FixedNow.AddDays(-2));
        await SeedLeagueAsync(nullEndsOnId, endsOn: null);

        var sut = Mocker.CreateInstance<LeagueDeactivationJob>();
        await sut.ExecuteAsync();

        var groups = await DataContext.PickemGroups.AsNoTracking().ToDictionaryAsync(g => g.Id);
        Assert.Equal(FixedNow, groups[eligibleId].DeactivatedUtc);
        Assert.Null(groups[withinWindowId].DeactivatedUtc);
        Assert.Null(groups[nullEndsOnId].DeactivatedUtc);
    }

    [Fact]
    public async Task ExecuteAsync_NoLeagues_CompletesWithoutError()
    {
        var sut = Mocker.CreateInstance<LeagueDeactivationJob>();

        // Should not throw with an empty table.
        await sut.ExecuteAsync();

        Assert.False(await DataContext.PickemGroups.AnyAsync());
    }

    private async Task SeedLeagueAsync(
        Guid leagueId,
        DateTime? endsOn,
        DateTime? deactivatedUtc = null)
    {
        var group = new PickemGroup
        {
            Id = leagueId,
            Name = $"Test League {leagueId:N}",
            Sport = Sport.BaseballMlb,
            League = League.MLB,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            CommissionerUserId = Guid.NewGuid(),
            EndsOn = endsOn,
            DeactivatedUtc = deactivatedUtc,
            CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = Guid.Empty,
        };

        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.SaveChangesAsync();
    }
}
