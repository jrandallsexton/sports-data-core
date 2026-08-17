using FluentAssertions;

using Moq;

using SportsData.Api.Application.Admin.SmackLab;
using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.Admin.SmackLab;

/// <summary>
/// The Lab's fact payloads must match what a live send saw — the derivation
/// here mirrors PickScoringProcessor, so the sign conventions and gates are
/// the behaviour worth pinning.
/// </summary>
public class GetSmackLabPicksQueryHandlerTests : ApiTestBase<GetSmackLabPicksQueryHandler>
{
    private static readonly DateTime FixedNow = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly Guid _homeFranchiseSeasonId = Guid.NewGuid();
    private readonly Guid _awayFranchiseSeasonId = Guid.NewGuid();
    private readonly Mock<IProvideContests> _contestClient = new();

    public GetSmackLabPicksQueryHandlerTests()
    {
        Mocker.GetMock<IContestClientFactory>()
            .Setup(f => f.Resolve(It.IsAny<Sport>()))
            .Returns(_contestClient.Object);
    }

    private void StubResult(Guid contestId, double? spread = null, bool finalized = true)
    {
        _contestClient
            .Setup(c => c.GetMatchupResult(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<MatchupResult>(new MatchupResult
            {
                ContestId = contestId,
                HomeFranchiseSeasonId = _homeFranchiseSeasonId,
                AwayFranchiseSeasonId = _awayFranchiseSeasonId,
                HomeScore = 24,
                AwayScore = 20,
                Spread = spread,
                HomeAbbreviation = "HOM",
                AwayAbbreviation = "AWY",
                FinalizedUtc = finalized ? FixedNow : null
            }));
    }

    private async Task<Guid> SeedLeagueAsync(PickType pickType)
    {
        var league = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Sluggers",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = pickType,
            CommissionerUserId = Guid.NewGuid(),
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };
        DataContext.PickemGroups.Add(league);
        await DataContext.SaveChangesAsync();
        return league.Id;
    }

    private async Task<Guid> SeedScoredPickAsync(
        Guid leagueId,
        Guid contestId,
        Guid? franchiseSeasonId,
        bool isCorrect = false)
    {
        var userId = Guid.NewGuid();
        DataContext.Users.Add(new UserEntity
        {
            Id = userId,
            FirebaseUid = $"fb-{userId:N}",
            Email = $"{userId:N}@example.com",
            SignInProvider = "password",
            DisplayName = "Rater McTester",
            Username = $"u{userId:N}"[..12],
            CreatedUtc = FixedNow,
            CreatedBy = userId
        });

        var pick = new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId,
            ContestId = contestId,
            Week = 1,
            FranchiseSeasonId = franchiseSeasonId,
            IsCorrect = isCorrect,
            CreatedUtc = FixedNow,
            CreatedBy = userId
        };
        DataContext.UserPicks.Add(pick);
        await DataContext.SaveChangesAsync();
        return pick.Id;
    }

    [Fact]
    public async Task UnknownLeague_ReturnsNotFound()
    {
        var handler = Mocker.CreateInstance<GetSmackLabPicksQueryHandler>();

        var result = await handler.ExecuteAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task AtsAwayPick_CarriesNegatedSpread_InBothFields()
    {
        // MatchupResult.Spread is home-perspective; an away pick negates it —
        // the exact PickScoringProcessor convention. ATS league ⇒ both
        // PickedSpread (display) and MarketSpread (situation) populate.
        var leagueId = await SeedLeagueAsync(PickType.AgainstTheSpread);
        var contestId = Guid.NewGuid();
        StubResult(contestId, spread: -6.5);
        await SeedScoredPickAsync(leagueId, contestId, _awayFranchiseSeasonId);

        var result = await Mocker.CreateInstance<GetSmackLabPicksQueryHandler>()
            .ExecuteAsync(leagueId);

        var facts = result.Value.Single().Facts;
        facts.PickedIsHome.Should().BeFalse();
        facts.PickedSpread.Should().Be(6.5);
        facts.MarketSpread.Should().Be(6.5);
        facts.AwayAbbreviation.Should().Be("AWY");
        facts.HomeScore.Should().Be(24);
    }

    [Fact]
    public async Task StraightUpPick_GetsMarketSpreadOnly()
    {
        // The gate that makes the flagship taunt possible: SU leagues carry
        // the line in MarketSpread while PickedSpread stays null so standard
        // copy never grows a spread.
        var leagueId = await SeedLeagueAsync(PickType.StraightUp);
        var contestId = Guid.NewGuid();
        StubResult(contestId, spread: 14);
        await SeedScoredPickAsync(leagueId, contestId, _homeFranchiseSeasonId);

        var result = await Mocker.CreateInstance<GetSmackLabPicksQueryHandler>()
            .ExecuteAsync(leagueId);

        var facts = result.Value.Single().Facts;
        facts.PickedSpread.Should().BeNull();
        facts.MarketSpread.Should().Be(14);
    }

    [Fact]
    public async Task OverUnderPick_HasNoSideAndNoSpreads()
    {
        var leagueId = await SeedLeagueAsync(PickType.OverUnder);
        var contestId = Guid.NewGuid();
        StubResult(contestId, spread: -3);
        await SeedScoredPickAsync(leagueId, contestId, franchiseSeasonId: null);

        var result = await Mocker.CreateInstance<GetSmackLabPicksQueryHandler>()
            .ExecuteAsync(leagueId);

        var dto = result.Value.Single();
        dto.Facts.PickedIsHome.Should().BeNull();
        dto.Facts.PickedSpread.Should().BeNull();
        dto.Facts.MarketSpread.Should().BeNull("no side means no picked-side perspective to sign the line from");
        dto.PickLabel.Should().Be("O/U");
    }

    [Fact]
    public async Task UnfinalizedContest_PicksAreSkippedNotFailed()
    {
        var leagueId = await SeedLeagueAsync(PickType.StraightUp);
        var goodContest = Guid.NewGuid();
        var unfinalized = Guid.NewGuid();
        StubResult(goodContest);
        StubResult(unfinalized, finalized: false);
        await SeedScoredPickAsync(leagueId, goodContest, _homeFranchiseSeasonId);
        await SeedScoredPickAsync(leagueId, unfinalized, _homeFranchiseSeasonId);

        var result = await Mocker.CreateInstance<GetSmackLabPicksQueryHandler>()
            .ExecuteAsync(leagueId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1, "one contest lacked a finalized result and its pick is skipped");
    }

    [Fact]
    public async Task UnscoredPicks_AreExcluded()
    {
        var leagueId = await SeedLeagueAsync(PickType.StraightUp);
        var contestId = Guid.NewGuid();
        StubResult(contestId);
        await SeedScoredPickAsync(leagueId, contestId, _homeFranchiseSeasonId);

        // Unscored pick (IsCorrect null) in the same league.
        DataContext.UserPicks.Add(new PickemGroupUserPick
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = Guid.NewGuid(),
            ContestId = contestId,
            Week = 1,
            FranchiseSeasonId = _homeFranchiseSeasonId,
            IsCorrect = null,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();

        var result = await Mocker.CreateInstance<GetSmackLabPicksQueryHandler>()
            .ExecuteAsync(leagueId);

        result.Value.Should().HaveCount(1, "there is nothing to preview for an unscored pick");
    }
}
