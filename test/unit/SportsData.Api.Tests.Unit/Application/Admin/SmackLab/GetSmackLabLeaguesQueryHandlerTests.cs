using FluentAssertions;

using SportsData.Api.Application.Admin.SmackLab;
using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.SmackLab;

/// <summary>
/// Pins the ordering/filtering behaviour. Honest caveat: the first cut of
/// this query ordered by a constructor-projected record property and threw
/// ONLY against real PostgreSQL — the InMemory provider is laxer about
/// translation, so these tests guard the logic, not SQL translatability.
/// Real translation proof stays with local E2E against Postgres.
/// </summary>
public class GetSmackLabLeaguesQueryHandlerTests : ApiTestBase<GetSmackLabLeaguesQueryHandler>
{
    private static readonly DateTime FixedNow = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);

    private async Task<Guid> SeedLeagueWithPicksAsync(string name, int scored, int unscored)
    {
        var league = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = name,
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = PickType.AgainstTheSpread,
            CommissionerUserId = Guid.NewGuid(),
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };
        DataContext.PickemGroups.Add(league);

        for (var i = 0; i < scored + unscored; i++)
        {
            DataContext.UserPicks.Add(new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                PickemGroupId = league.Id,
                UserId = Guid.NewGuid(),
                ContestId = Guid.NewGuid(),
                Week = 1,
                IsCorrect = i < scored ? (i % 2 == 0) : null,
                CreatedUtc = FixedNow,
                CreatedBy = Guid.NewGuid()
            });
        }
        await DataContext.SaveChangesAsync();
        return league.Id;
    }

    [Fact]
    public async Task ReturnsOnlyLeaguesWithScoredPicks_CountedAndOrdered()
    {
        var small = await SeedLeagueWithPicksAsync("Small", scored: 2, unscored: 3);
        var big = await SeedLeagueWithPicksAsync("Big", scored: 5, unscored: 0);
        await SeedLeagueWithPicksAsync("Unscored only", scored: 0, unscored: 4);

        var result = await Mocker.CreateInstance<GetSmackLabLeaguesQueryHandler>()
            .ExecuteAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2, "a league with no scored picks has nothing to preview");
        result.Value[0].LeagueId.Should().Be(big, "most scored picks first");
        result.Value[0].ScoredPickCount.Should().Be(5);
        result.Value[1].LeagueId.Should().Be(small);
        result.Value[1].ScoredPickCount.Should().Be(2, "unscored picks must not inflate the count");
        result.Value[1].Sport.Should().Be(nameof(Sport.FootballNcaa));
        result.Value[1].PickType.Should().Be(nameof(PickType.AgainstTheSpread));
    }

    [Fact]
    public async Task NoScoredPicksAnywhere_ReturnsEmptyListNotError()
    {
        await SeedLeagueWithPicksAsync("Fresh league", scored: 0, unscored: 2);

        var result = await Mocker.CreateInstance<GetSmackLabLeaguesQueryHandler>()
            .ExecuteAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
