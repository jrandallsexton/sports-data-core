using FluentAssertions;

using SportsData.Producer.Application.FranchiseContestFacts;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseContestFacts;

/// <summary>
/// The derivation rules for the War Room team-game rows. Real IANA zones are
/// used, so the local-time cases also prove the zone data resolves.
/// </summary>
public class FranchiseContestFactProjectorTests
{
    private static readonly Guid HomeId = Guid.NewGuid();
    private static readonly Guid AwayId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Project_ReturnsHomeThenAway_WithResultSpreadAndAtsFromEachSide()
    {
        // Home favored by 7 (home-relative -7), wins 31-21, covers.
        var input = Game() with
        {
            HomeScore = 31,
            AwayScore = 21,
            WinnerFranchiseSeasonId = HomeId,
            SpreadWinnerFranchiseSeasonId = HomeId,
            HomeSpread = -7m,
            OverUnder = 48.5m,
            TotalResult = OverUnderResult.Over,
        };

        var facts = FranchiseContestFactProjector.Project(input, Now);

        facts.Should().HaveCount(2);
        var (home, away) = (facts[0], facts[1]);

        home.FranchiseSeasonId.Should().Be(HomeId);
        home.OpponentFranchiseSeasonId.Should().Be(AwayId);
        home.Site.Should().Be(GameSite.Home);
        home.PointsFor.Should().Be(31);
        home.PointsAgainst.Should().Be(21);
        home.Margin.Should().Be(10);
        home.Result.Should().Be(GameResult.Win);
        home.Spread.Should().Be(-7m);
        home.AtsResult.Should().Be(AtsResult.Covered);
        home.TotalResult.Should().Be(OverUnderResult.Over);
        home.OverUnder.Should().Be(48.5m);

        away.Site.Should().Be(GameSite.Away);
        away.Margin.Should().Be(-10);
        away.Result.Should().Be(GameResult.Loss);
        away.Spread.Should().Be(7m);
        away.AtsResult.Should().Be(AtsResult.NotCovered);

        home.ProjectionVersion.Should().Be(FranchiseContestFact.CurrentProjectionVersion);
        home.ProjectedUtc.Should().Be(Now);
    }

    [Fact]
    public void Project_NeutralSite_BothSidesNeutral()
    {
        var facts = FranchiseContestFactProjector.Project(Game() with { IsNeutralSite = true }, Now);

        facts.Select(f => f.Site).Should().AllBeEquivalentTo(GameSite.Neutral);
    }

    [Fact]
    public void Project_PushClaimedOnlyWhenLineAndMarginShowOne()
    {
        // No spread winner (push or unsourced); home -7, won by 7.
        var push = FranchiseContestFactProjector.Project(Game() with
        {
            HomeScore = 24, AwayScore = 17, WinnerFranchiseSeasonId = HomeId, HomeSpread = -7m,
        }, Now);
        push.Select(f => f.AtsResult).Should().AllBeEquivalentTo(AtsResult.Push);

        // No spread winner and the margin does not match the line: unsourced, not a push.
        var unsourced = FranchiseContestFactProjector.Project(Game() with
        {
            HomeScore = 24, AwayScore = 10, WinnerFranchiseSeasonId = HomeId, HomeSpread = -7m,
        }, Now);
        unsourced.Select(f => f.AtsResult).Should().AllBeEquivalentTo((AtsResult?)null);
    }

    [Fact]
    public void Project_TieAndMissingScores()
    {
        var tie = FranchiseContestFactProjector.Project(Game() with { HomeScore = 17, AwayScore = 17 }, Now);
        tie.Select(f => f.Result).Should().AllBeEquivalentTo(GameResult.Tie);

        var noScores = FranchiseContestFactProjector.Project(Game(), Now);
        noScores.Select(f => f.Result).Should().AllBeEquivalentTo((GameResult?)null);
        noScores.Select(f => f.Margin).Should().AllBeEquivalentTo((int?)null);
        noScores[0].TotalResult.Should().BeNull("OverUnderResult.None is no result");
    }

    [Fact]
    public void Project_LocalKickoff_UsesVenueZone_AndLocalCalendarDate()
    {
        // 01:00Z Sunday Dec 1 = 20:00 Saturday Nov 30 in Indianapolis (EST, -5).
        var facts = FranchiseContestFactProjector.Project(Game() with
        {
            KickoffUtc = new DateTime(2024, 12, 1, 1, 0, 0, DateTimeKind.Utc),
            VenueTimeZone = "America/Indiana/Indianapolis",
        }, Now);

        var home = facts[0];
        home.KickoffLocal.Should().Be(new DateTime(2024, 11, 30, 20, 0, 0));
        home.KickoffWindow.Should().Be(KickoffWindow.Night);
        home.LocalMonth.Should().Be(11);
        home.LocalDayOfWeek.Should().Be((int)DayOfWeek.Saturday);
    }

    [Theory]
    [InlineData(16, 0, KickoffWindow.Noon)]       // 12:00 EDT
    [InlineData(17, 59, KickoffWindow.Noon)]      // 13:59 EDT
    [InlineData(18, 0, KickoffWindow.Afternoon)]  // 14:00 EDT
    [InlineData(21, 59, KickoffWindow.Afternoon)] // 17:59 EDT
    [InlineData(22, 0, KickoffWindow.Night)]      // 18:00 EDT
    public void Project_KickoffWindowBoundaries(int utcHour, int utcMinute, KickoffWindow expected)
    {
        var facts = FranchiseContestFactProjector.Project(Game() with
        {
            KickoffUtc = new DateTime(2025, 9, 13, utcHour, utcMinute, 0, DateTimeKind.Utc),
            VenueTimeZone = "America/New_York",
        }, Now);

        facts[0].KickoffWindow.Should().Be(expected);
    }

    [Fact]
    public void Project_DateOnlyKickoff_HasNoTimeOrWindow_AndKeepsThePublishedDate()
    {
        // 07:00Z placeholder; converting would make it 21:00 Nov 30 in Honolulu.
        var facts = FranchiseContestFactProjector.Project(Game() with
        {
            KickoffUtc = new DateTime(2001, 12, 1, 7, 0, 0, DateTimeKind.Utc),
            KickoffTimeKnown = false,
            VenueTimeZone = "Pacific/Honolulu",
        }, Now);

        var home = facts[0];
        home.KickoffTimeKnown.Should().BeFalse();
        home.KickoffLocal.Should().BeNull();
        home.KickoffWindow.Should().BeNull();
        home.LocalMonth.Should().Be(12);
        home.LocalDayOfWeek.Should().Be((int)DayOfWeek.Saturday);
    }

    [Fact]
    public void Project_NoVenueZone_HasNoLocalFields()
    {
        var home = FranchiseContestFactProjector.Project(Game() with { VenueTimeZone = null }, Now)[0];

        home.KickoffLocal.Should().BeNull();
        home.KickoffWindow.Should().BeNull();
        home.LocalMonth.Should().BeNull();
        home.LocalDayOfWeek.Should().BeNull();
    }

    [Fact]
    public void Project_ConferenceGame_UsesEspnFlag_WhenSeasonPublishesIt()
    {
        var conference = Guid.NewGuid();
        var game = Game(homeConference: conference, awayConference: conference) with
        {
            SeasonUsesConferenceFlag = true,
            EspnConferenceFlag = false,
        };

        FranchiseContestFactProjector.Project(game, Now)
            .Should().OnlyContain(f => !f.IsConferenceGame, "ESPN's flag wins where the season publishes it");
    }

    [Fact]
    public void Project_ConferenceGame_FallsBackToSharedConference_OutsidePostseasonAndTitleGames()
    {
        var conference = Guid.NewGuid();
        var game = Game(homeConference: conference, awayConference: conference) with { SeasonUsesConferenceFlag = false };

        FranchiseContestFactProjector.Project(game, Now).Should().OnlyContain(f => f.IsConferenceGame);
        FranchiseContestFactProjector.Project(game with { IsChampionship = true }, Now)
            .Should().OnlyContain(f => !f.IsConferenceGame);
        FranchiseContestFactProjector.Project(game with { SeasonPhaseTypeCode = 3 }, Now)
            .Should().OnlyContain(f => !f.IsConferenceGame);
        FranchiseContestFactProjector.Project(
                Game(homeConference: conference, awayConference: Guid.NewGuid()) with { SeasonUsesConferenceFlag = false }, Now)
            .Should().OnlyContain(f => !f.IsConferenceGame);
    }

    [Fact]
    public void Project_GroupingRankRecordAndBox_ComeFromEachTeam()
    {
        var game = Game() with
        {
            Home = Team(HomeId, "NCAAF|NCAA|fbs|sec") with
            {
                CuratedRank = 5,
                WinsBefore = 8,
                LossesBefore = 1,
                Box = new TeamBoxInput { TotalYards = 455, SacksMade = 3, SacksAllowed = 1 },
            },
            Away = Team(AwayId, "NCAAF|yy") with { CuratedRank = 99, WinsBefore = 4, LossesBefore = 5, TiesBefore = 1 },
        };

        var (home, away) = (FranchiseContestFactProjector.Project(game, Now)[0], FranchiseContestFactProjector.Project(game, Now)[1]);

        home.Division.Should().Be("fbs");
        home.Conference.Should().Be("sec");
        home.OpponentDivision.Should().BeNull();
        home.OpponentConference.Should().BeNull();
        home.RankGoingIn.Should().Be(5);
        home.OpponentRankGoingIn.Should().Be(99);
        home.WinsBefore.Should().Be(8);
        home.LossesBefore.Should().Be(1);
        home.OpponentWinsBefore.Should().Be(4);
        home.OpponentTiesBefore.Should().Be(1);
        home.TotalYards.Should().Be(455);
        home.SacksMade.Should().Be(3);

        away.WinsBefore.Should().Be(4);
        away.OpponentRankGoingIn.Should().Be(5);
        away.TotalYards.Should().BeNull("no box score means unknown, not zero");
    }

    [Fact]
    public void Project_NflGroupSeasonMap_IsConferenceThenDivision()
    {
        var game = Game() with
        {
            Home = Team(HomeId, "NFL|NFC|NORTH"),
            Away = Team(AwayId, "NFL|AFC|EAST"),
        };

        var home = FranchiseContestFactProjector.Project(game, Now)[0];

        home.Conference.Should().Be("NFC");
        home.Division.Should().Be("NFC NORTH", "the division name alone is ambiguous across conferences");
        home.OpponentConference.Should().Be("AFC");
        home.OpponentDivision.Should().Be("AFC EAST");
        home.ProjectionVersion.Should().Be(2);
    }

    private static ContestFactInput Game(Guid? homeConference = null, Guid? awayConference = null) => new()
    {
        ContestId = Guid.NewGuid(),
        SeasonYear = 2025,
        Week = 10,
        SeasonPhaseTypeCode = 2,
        KickoffUtc = new DateTime(2025, 11, 1, 19, 30, 0, DateTimeKind.Utc),
        KickoffTimeKnown = true,
        VenueTimeZone = "America/Chicago",
        Home = Team(HomeId, "NCAAF|NCAA|fbs|sec") with { ConferenceGroupId = homeConference },
        Away = Team(AwayId, "NCAAF|NCAA|fbs|sec") with { ConferenceGroupId = awayConference },
    };

    private static TeamFactInput Team(Guid franchiseSeasonId, string groupSeasonMap) => new()
    {
        FranchiseSeasonId = franchiseSeasonId,
        FranchiseId = Guid.NewGuid(),
        GroupSeasonMap = groupSeasonMap,
    };
}
