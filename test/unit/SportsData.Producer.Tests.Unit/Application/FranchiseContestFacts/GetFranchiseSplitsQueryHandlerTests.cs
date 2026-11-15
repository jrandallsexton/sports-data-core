using FluentAssertions;

using FluentValidation;

using Moq;

using SportsData.Core.Common;
using SportsData.Producer.Application.FranchiseContestFacts.Queries.GetFranchiseSplits;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseContestFacts;

/// <summary>
/// War Room Splits over FranchiseContestFact. Rows are handcrafted: each test
/// states only the columns its filter or aggregate reads.
/// </summary>
public class GetFranchiseSplitsQueryHandlerTests : ProducerTestBase<GetFranchiseSplitsQueryHandler>
{
    private static readonly Guid LsuId = Guid.NewGuid();
    private static readonly Guid AlabamaId = Guid.NewGuid();
    private static readonly Guid OleMissId = Guid.NewGuid();

    public GetFranchiseSplitsQueryHandlerTests()
    {
        // Real validator: an auto-mocked IValidator returns a null result.
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(x => x.UtcNow())
            .Returns(new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc));
        Mocker.Use<IValidator<GetFranchiseSplitsQuery>>(
            new GetFranchiseSplitsQueryValidator(dateTimeProvider.Object));

        SeedFranchise(LsuId, "lsu-tigers", "LSU Tigers");
        SeedFranchise(AlabamaId, "alabama-crimson-tide", "Alabama Crimson Tide");
        SeedFranchise(OleMissId, "ole-miss-rebels", "Ole Miss Rebels");
    }

    [Fact]
    public async Task Execute_NoFilters_SummarizesTeamGamesNewestFirst()
    {
        SeedFact(2024, AlabamaId, new DateTime(2024, 11, 9, 0, 30, 0), pf: 13, pa: 42, GameResult.Loss, spread: 2.5m, AtsResult.NotCovered);
        SeedFact(2025, OleMissId, new DateTime(2025, 9, 27, 16, 0, 0), pf: 19, pa: 24, GameResult.Loss, spread: 0m, AtsResult.NotCovered);
        SeedFact(2025, AlabamaId, new DateTime(2025, 11, 8, 23, 0, 0), pf: 20, pa: 9, GameResult.Win, spread: -3m, AtsResult.Covered);
        SeedFact(2023, OleMissId, new DateTime(2023, 9, 30, 22, 0, 0), pf: 49, pa: 55, GameResult.Loss, spread: null, atsResult: null);
        // Another team's row never leaks in.
        SeedFact(2025, LsuId, new DateTime(2025, 11, 8, 23, 0, 0), pf: 9, pa: 20, GameResult.Loss, spread: 3m, AtsResult.NotCovered, franchiseId: AlabamaId);
        await FootballDataContext.SaveChangesAsync();

        var result = await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2001, 2025));

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.FranchiseName.Should().Be("LSU Tigers");
        dto.SeasonFrom.Should().Be(2001);
        dto.Games.Select(g => g.SeasonYear).Should().Equal(2025, 2025, 2024, 2023);
        dto.Games[0].OpponentName.Should().Be("Alabama Crimson Tide");
        dto.Games[0].OpponentSlug.Should().Be("alabama-crimson-tide");
        dto.Games[0].Result.Should().Be("Win");
        dto.Games[0].AtsResult.Should().Be("Covered");
        dto.Games[0].Site.Should().Be("Home");

        var s = dto.Summary;
        s.Games.Should().Be(4);
        (s.Wins, s.Losses, s.Ties).Should().Be((1, 3, 0));
        (s.AtsWins, s.AtsLosses, s.AtsPushes).Should().Be((1, 2, 0));
        s.AvgPointsFor.Should().Be(25.3m);   // (20 + 19 + 13 + 49) / 4
        s.AvgPointsAgainst.Should().Be(32.5m); // (9 + 24 + 42 + 55) / 4
        s.AvgMargin.Should().Be(-7.3m);      // -29 / 4 = -7.25, rounded away from zero
        // Pick'em (0) and no line count as neither favorite nor underdog.
        (s.FavoriteWins, s.FavoriteLosses).Should().Be((1, 0));
        (s.UnderdogWins, s.UnderdogLosses).Should().Be((0, 1));
    }

    [Fact]
    public async Task Execute_OpponentRank_UnknownRankMatchesNeitherRankedNorUnranked()
    {
        SeedFact(2025, AlabamaId, new DateTime(2025, 11, 1), opponentRank: 4);
        SeedFact(2025, OleMissId, new DateTime(2025, 10, 1), opponentRank: 18);
        SeedFact(2024, OleMissId, new DateTime(2024, 10, 1), opponentRank: 99);
        SeedFact(2003, AlabamaId, new DateTime(2003, 10, 1), opponentRank: null);
        await FootballDataContext.SaveChangesAsync();

        (await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2001, 2025, OpponentRank: OpponentRankFilter.Ranked)))
            .Value.Games.Select(g => g.OpponentRankGoingIn).Should().Equal(4, 18);
        (await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2001, 2025, OpponentRank: OpponentRankFilter.Top10)))
            .Value.Games.Select(g => g.OpponentRankGoingIn).Should().Equal(4);
        (await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2001, 2025, OpponentRank: OpponentRankFilter.Unranked)))
            .Value.Games.Select(g => g.OpponentRankGoingIn).Should().Equal(99);
    }

    [Fact]
    public async Task Execute_SiteWindowAndConferenceFilters_Combine()
    {
        SeedFact(2025, AlabamaId, new DateTime(2025, 11, 8), site: GameSite.Away, window: KickoffWindow.Night, conferenceGame: true);
        SeedFact(2025, OleMissId, new DateTime(2025, 9, 27), site: GameSite.Away, window: KickoffWindow.Noon, conferenceGame: true);
        SeedFact(2024, AlabamaId, new DateTime(2024, 11, 9), site: GameSite.Home, window: KickoffWindow.Night, conferenceGame: true);
        SeedFact(2024, OleMissId, new DateTime(2024, 9, 1), site: GameSite.Away, window: KickoffWindow.Night, conferenceGame: false);
        // Date-only kickoff: no window, never matches a window filter.
        SeedFact(2023, AlabamaId, new DateTime(2023, 11, 4), site: GameSite.Away, window: null, conferenceGame: true);
        await FootballDataContext.SaveChangesAsync();

        var result = await Execute(new GetFranchiseSplitsQuery(
            "lsu-tigers", 2014, 2025,
            Site: GameSite.Away,
            KickoffWindow: KickoffWindow.Night,
            ConferenceGame: true));

        result.Value.Games.Should().ContainSingle()
            .Which.KickoffUtc.Should().Be(new DateTime(2025, 11, 8, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Execute_LineFilter_PickemAndNoLineExcluded()
    {
        SeedFact(2025, AlabamaId, new DateTime(2025, 11, 8), spread: -3m);
        SeedFact(2025, OleMissId, new DateTime(2025, 9, 27), spread: 0m);
        SeedFact(2024, AlabamaId, new DateTime(2024, 11, 9), spread: 2.5m);
        SeedFact(2024, OleMissId, new DateTime(2024, 9, 28), spread: null);
        await FootballDataContext.SaveChangesAsync();

        (await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2012, 2025, Line: LineFilter.Favorite)))
            .Value.Games.Select(g => g.Spread).Should().Equal(-3m);
        (await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2012, 2025, Line: LineFilter.Underdog)))
            .Value.Games.Select(g => g.Spread).Should().Equal(2.5m);
    }

    [Fact]
    public async Task Execute_FilterRaisesSeasonFloor_ReportsEffectiveStart()
    {
        SeedFact(2010, AlabamaId, new DateTime(2010, 11, 6), spread: -3m);
        SeedFact(2013, AlabamaId, new DateTime(2013, 11, 9), spread: -6m);
        await FootballDataContext.SaveChangesAsync();

        var result = await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2005, 2025, Line: LineFilter.Favorite));

        result.Value.RequestedSeasonFrom.Should().Be(2005);
        result.Value.SeasonFrom.Should().Be(2012);
        result.Value.Games.Select(g => g.SeasonYear).Should().Equal(2013);
    }

    [Fact]
    public async Task Execute_FloorAboveRange_ReturnsEmpty()
    {
        SeedFact(2010, AlabamaId, new DateTime(2010, 11, 6), conferenceGame: true);
        await FootballDataContext.SaveChangesAsync();

        var result = await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2005, 2010, ConferenceGame: true));

        result.IsSuccess.Should().BeTrue();
        result.Value.SeasonFrom.Should().Be(2014);
        result.Value.Games.Should().BeEmpty();
        result.Value.Summary.Games.Should().Be(0);
        result.Value.Summary.AvgMargin.Should().BeNull();
    }

    [Fact]
    public async Task Execute_UnknownSlug_NotFound()
    {
        var result = await Execute(new GetFranchiseSplitsQuery("no-such-team", 2001, 2025));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Execute_SeasonToBeforeSeasonFrom_Validation()
    {
        var result = await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2020, 2015));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    private Task<Result<Core.Dtos.Canonical.FranchiseSplitsDto>> Execute(GetFranchiseSplitsQuery query) =>
        Mocker.CreateInstance<GetFranchiseSplitsQueryHandler>().ExecuteAsync(query);

    private void SeedFranchise(Guid id, string slug, string displayName)
    {
        FootballDataContext.Franchises.Add(new Franchise
        {
            Id = id,
            Sport = Sport.FootballNcaa,
            Name = displayName,
            Location = displayName,
            DisplayName = displayName,
            DisplayNameShort = displayName,
            ColorCodeHex = "000000",
            Slug = slug,
        });
    }

    private void SeedFact(
        int seasonYear,
        Guid opponentId,
        DateTime kickoffUtc,
        int? pf = 21,
        int? pa = 14,
        GameResult? result = GameResult.Win,
        decimal? spread = null,
        AtsResult? atsResult = null,
        int? opponentRank = 99,
        GameSite site = GameSite.Home,
        KickoffWindow? window = KickoffWindow.Afternoon,
        bool conferenceGame = false,
        Guid? franchiseId = null)
    {
        FootballDataContext.FranchiseContestFacts.Add(new FranchiseContestFact
        {
            ContestId = Guid.NewGuid(),
            FranchiseSeasonId = Guid.NewGuid(),
            FranchiseId = franchiseId ?? LsuId,
            OpponentFranchiseSeasonId = Guid.NewGuid(),
            OpponentFranchiseId = franchiseId.HasValue ? LsuId : opponentId,
            SeasonYear = seasonYear,
            KickoffUtc = DateTime.SpecifyKind(kickoffUtc, DateTimeKind.Utc),
            KickoffTimeKnown = window.HasValue,
            KickoffWindow = window,
            Site = site,
            IsConferenceGame = conferenceGame,
            OpponentRankGoingIn = opponentRank,
            PointsFor = pf,
            PointsAgainst = pa,
            Margin = pf - pa,
            Result = result,
            Spread = spread,
            AtsResult = atsResult,
        });
    }
}
