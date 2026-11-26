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
        SeedFact(2010, AlabamaId, new DateTime(2010, 11, 6), spread: -3m);
        await FootballDataContext.SaveChangesAsync();

        var result = await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2005, 2010, Line: LineFilter.Favorite));

        result.IsSuccess.Should().BeTrue();
        result.Value.SeasonFrom.Should().Be(2012);
        result.Value.Games.Should().BeEmpty();
        result.Value.Summary.Games.Should().Be(0);
        result.Value.Summary.AvgMargin.Should().BeNull();
    }

    [Fact]
    public async Task Execute_ConferenceFilter_ReachesBack_To2001()
    {
        SeedFact(2006, AlabamaId, new DateTime(2006, 11, 4), conferenceGame: true);
        SeedFact(2006, OleMissId, new DateTime(2007, 1, 1), conferenceGame: false);
        await FootballDataContext.SaveChangesAsync();

        var nonConference = await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2001, 2025, ConferenceGame: false));

        nonConference.Value.SeasonFrom.Should().Be(2001);
        nonConference.Value.Games.Select(g => g.KickoffUtc).Should().Equal(new DateTime(2007, 1, 1));
    }

    [Fact]
    public async Task Execute_OpponentConference_MatchesConferenceInThatSeason()
    {
        // Texas A&M-style move: the same opponent was big12, then sec.
        SeedFact(2011, AlabamaId, new DateTime(2011, 11, 5), opponentConference: "big12");
        SeedFact(2013, AlabamaId, new DateTime(2013, 11, 9), opponentConference: "sec");
        SeedFact(2014, OleMissId, new DateTime(2014, 10, 25), opponentConference: "sec");
        SeedFact(2015, OleMissId, new DateTime(2015, 9, 5), opponentConference: null);
        await FootballDataContext.SaveChangesAsync();

        (await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2001, 2025, OpponentConference: "sec")))
            .Value.Games.Select(g => g.SeasonYear).Should().Equal(2014, 2013);
        (await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2001, 2025, OpponentConference: "big12")))
            .Value.Games.Select(g => g.SeasonYear).Should().Equal(2011);
    }

    [Fact]
    public async Task Execute_Opponent_HeadToHeadAcrossSeasons()
    {
        SeedFact(2003, AlabamaId, new DateTime(2003, 11, 8));
        SeedFact(2019, AlabamaId, new DateTime(2019, 11, 9));
        SeedFact(2019, OleMissId, new DateTime(2019, 11, 16));
        await FootballDataContext.SaveChangesAsync();

        var result = await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2001, 2025, Opponent: "alabama-crimson-tide"));

        result.Value.Games.Select(g => g.SeasonYear).Should().Equal(2019, 2003);
        result.Value.Games.Should().OnlyContain(g => g.OpponentSlug == "alabama-crimson-tide");
    }

    [Fact]
    public async Task Execute_UnknownOpponent_MatchesNothing()
    {
        SeedFact(2019, AlabamaId, new DateTime(2019, 11, 9));
        await FootballDataContext.SaveChangesAsync();

        var result = await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2001, 2025, Opponent: "no-such-team"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Games.Should().BeEmpty();
        // The pickers still list what the team actually played.
        result.Value.Options.Opponents.Should().ContainSingle(o => o.Slug == "alabama-crimson-tide");
    }

    [Fact]
    public async Task Execute_SeasonType_RegularAndPostseason_AllStarMatchesNeither()
    {
        SeedFact(2019, AlabamaId, new DateTime(2019, 11, 9), phase: 2);
        SeedFact(2019, OleMissId, new DateTime(2020, 1, 1), phase: 3);
        SeedFact(2014, OleMissId, new DateTime(2015, 1, 24), phase: 4);
        await FootballDataContext.SaveChangesAsync();

        (await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2001, 2025, SeasonType: SeasonTypeFilter.Regular)))
            .Value.Games.Select(g => g.SeasonPhaseTypeCode).Should().Equal(2);
        (await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2001, 2025, SeasonType: SeasonTypeFilter.Postseason)))
            .Value.Games.Select(g => g.SeasonPhaseTypeCode).Should().Equal(3);
    }

    [Fact]
    public async Task Execute_Month_UsesLocalMonth_NoVenueMatchesNone()
    {
        // 03:00 UTC Dec 1 is a local November night game.
        SeedFact(2019, AlabamaId, new DateTime(2019, 12, 1, 3, 0, 0), localMonth: 11);
        SeedFact(2019, OleMissId, new DateTime(2019, 10, 12), localMonth: 10);
        SeedFact(2018, OleMissId, new DateTime(2018, 11, 3), localMonth: null);
        await FootballDataContext.SaveChangesAsync();

        (await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2001, 2025, Month: 11)))
            .Value.Games.Select(g => g.SeasonYear).Should().Equal(2019);
    }

    [Theory]
    [InlineData(SpreadSizeFilter.Close, new[] { -3.0, 0.0, 3.0 })]
    [InlineData(SpreadSizeFilter.Moderate, new[] { -6.5, -3.5, 3.5, 6.5 })]
    [InlineData(SpreadSizeFilter.Large, new[] { -13.5, -7.0, 7.0, 13.5 })]
    [InlineData(SpreadSizeFilter.Huge, new[] { -14.0, 14.0, 21.0 })]
    public async Task Execute_SpreadSize_BandsOnAbsoluteLine(SpreadSizeFilter size, double[] expected)
    {
        var spreads = new[] { -14m, -13.5m, -7m, -6.5m, -3.5m, -3m, 0m, 3m, 3.5m, 6.5m, 7m, 13.5m, 14m, 21m };
        var day = 0;
        foreach (var spread in spreads)
            SeedFact(2020, AlabamaId, new DateTime(2020, 9, 1).AddDays(day++), spread: spread);
        SeedFact(2020, OleMissId, new DateTime(2020, 12, 1), spread: null);
        await FootballDataContext.SaveChangesAsync();

        var result = await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2012, 2025, SpreadSize: size));

        result.Value.Games.Select(g => g.Spread!.Value).OrderBy(x => x)
            .Should().Equal(expected.Select(x => (decimal)x));
    }

    [Fact]
    public async Task Execute_SpreadSize_RaisesFloorTo2012()
    {
        SeedFact(2010, AlabamaId, new DateTime(2010, 11, 6), spread: -21m);
        SeedFact(2015, AlabamaId, new DateTime(2015, 11, 7), spread: -21m);
        await FootballDataContext.SaveChangesAsync();

        var result = await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2001, 2025, SpreadSize: SpreadSizeFilter.Huge));

        result.Value.SeasonFrom.Should().Be(2012);
        result.Value.Games.Select(g => g.SeasonYear).Should().Equal(2015);
    }

    [Theory]
    [InlineData(AtsResult.Covered, 2020)]
    [InlineData(AtsResult.NotCovered, 2021)]
    [InlineData(AtsResult.Push, 2022)]
    public async Task Execute_Ats_FiltersByOutcome(AtsResult ats, int expectedSeason)
    {
        SeedFact(2020, AlabamaId, new DateTime(2020, 11, 7), spread: -7m, atsResult: AtsResult.Covered);
        SeedFact(2021, AlabamaId, new DateTime(2021, 11, 6), spread: -7m, atsResult: AtsResult.NotCovered);
        SeedFact(2022, AlabamaId, new DateTime(2022, 11, 5), spread: -7m, atsResult: AtsResult.Push);
        // No line, so no outcome: matches none of them.
        SeedFact(2023, OleMissId, new DateTime(2023, 11, 4), spread: null, atsResult: null);
        await FootballDataContext.SaveChangesAsync();

        (await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2012, 2025, Ats: ats)))
            .Value.Games.Select(g => g.SeasonYear).Should().Equal(expectedSeason);
    }

    [Fact]
    public async Task Execute_Ats_RaisesFloorTo2012()
    {
        SeedFact(2010, AlabamaId, new DateTime(2010, 11, 6), spread: -7m, atsResult: AtsResult.Covered);
        SeedFact(2015, AlabamaId, new DateTime(2015, 11, 7), spread: -7m, atsResult: AtsResult.Covered);
        await FootballDataContext.SaveChangesAsync();

        var result = await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2001, 2025, Ats: AtsResult.Covered));

        result.Value.SeasonFrom.Should().Be(2012);
        result.Value.Games.Select(g => g.SeasonYear).Should().Equal(2015);
    }

    [Fact]
    public async Task Execute_Options_IgnoreChipFiltersButHonorSeasonRange()
    {
        SeedGroupSeason("midam", "MAC", 2025);
        SeedGroupSeason("sec", "SEC", 2010);
        SeedGroupSeason("sec", "SEC", 2025);
        SeedFact(2010, AlabamaId, new DateTime(2010, 11, 6), opponentConference: "sec");
        SeedFact(2019, AlabamaId, new DateTime(2019, 11, 9), opponentConference: "sec");
        SeedFact(2019, OleMissId, new DateTime(2019, 9, 7), opponentConference: "midam");
        SeedFact(2019, OleMissId, new DateTime(2019, 9, 14), opponentConference: "xyz");
        // Outside the requested range: not an option.
        SeedFact(2005, OleMissId, new DateTime(2005, 9, 3), opponentConference: "wac");
        await FootballDataContext.SaveChangesAsync();

        // The Line chip raises the search floor to 2012, but the options do not move.
        var result = await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2008, 2025, Line: LineFilter.Favorite));

        var options = result.Value.Options;
        options.Opponents.Select(o => (o.Slug, o.Games)).Should().Equal(
            ("alabama-crimson-tide", 2),
            ("ole-miss-rebels", 2));
        options.Conferences.Select(c => (c.Value, c.Label, c.Games)).Should().Equal(
            ("midam", "MAC", 1),
            ("sec", "SEC", 2),
            ("xyz", "xyz", 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task Execute_MonthOutOfRange_Validation(int month)
    {
        var result = await Execute(new GetFranchiseSplitsQuery("lsu-tigers", 2001, 2025, Month: month));

        result.Status.Should().Be(ResultStatus.Validation);
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

    private void SeedGroupSeason(string abbreviation, string shortName, int seasonYear)
    {
        FootballDataContext.GroupSeasons.Add(new GroupSeason
        {
            Id = Guid.NewGuid(),
            SeasonYear = seasonYear,
            Name = shortName,
            Slug = abbreviation,
            Abbreviation = abbreviation,
            ShortName = shortName,
            IsConference = true,
            CreatedBy = Guid.Empty,
            CreatedUtc = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc),
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
        Guid? franchiseId = null,
        string opponentConference = null,
        int? phase = 2,
        int? localMonth = null)
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
            OpponentConference = opponentConference,
            SeasonPhaseTypeCode = phase,
            LocalMonth = localMonth,
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
