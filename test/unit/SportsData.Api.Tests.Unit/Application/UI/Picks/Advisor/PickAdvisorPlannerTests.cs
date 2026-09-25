using FluentAssertions;

using SportsData.Api.Application.UI.Picks.Advisor;
using SportsData.Api.Application.UI.Picks.Advisor.Planner;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Picks.Advisor;

public class PickAdvisorPlannerTests
{
    private static readonly DateTime Kickoff = new(2026, 9, 26, 16, 0, 0, DateTimeKind.Utc);

    private readonly PickAdvisorPlanner _planner = new(new PickAdvisorOptions());

    /// <summary>A game where the model gives HOME the given probability. Home/away ids are derived from the contest id so tests can find them.</summary>
    private static PickAdvisorMatchup Game(
        Guid contestId,
        double pHome,
        Guid? preview = null,
        bool locked = false,
        PickAdvisorExistingPick? existing = null,
        int kickoffOffsetHours = 0,
        bool noModel = false)
    {
        var (home, away) = Sides(contestId);
        return new PickAdvisorMatchup(
            contestId, "Away @ Home", Kickoff.AddHours(kickoffOffsetHours), locked,
            home, away,
            noModel ? null : new PickAdvisorSignal(home, pHome),
            preview, existing);
    }

    private static (Guid Home, Guid Away) Sides(Guid contestId)
    {
        var bytes = contestId.ToByteArray();
        bytes[0] ^= 0x01;
        var home = new Guid(bytes);
        bytes[0] ^= 0x03;
        var away = new Guid(bytes);
        return (home, away);
    }

    private static Guid Home(Guid contestId) => Sides(contestId).Home;
    private static Guid Away(Guid contestId) => Sides(contestId).Away;

    private static PickAdvisorPlanInput Input(AdvisorLevel level, params PickAdvisorMatchup[] games) =>
        new(level, UseConfidencePoints: true, games);

    private static AdvisedPick Pick(PickAdvisorSheet sheet, Guid contestId) =>
        sheet.Picks.Single(p => p.ContestId == contestId);

    [Fact]
    public void Prevent_TakesModelSideEverywhere_PointsRankedByCertainty()
    {
        var g = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var sheet = _planner.BuildSheet(Input(AdvisorLevel.Prevent,
            Game(g[0], 0.55), Game(g[1], 0.90), Game(g[2], 0.52), Game(g[3], 0.80), Game(g[4], 0.70)));

        sheet.FlipCount.Should().Be(0);
        sheet.CoinFlipCount.Should().Be(2);
        sheet.Picks.Should().HaveCount(5);
        sheet.Picks.Select(p => p.ContestId).Should().Equal(g); // slate order

        Pick(sheet, g[1]).Should().BeEquivalentTo(new { FranchiseSeasonId = Home(g[1]), ConfidencePoints = 5, Kind = AdvisedPickKind.Lock, ModelProbability = 0.90 });
        Pick(sheet, g[3]).ConfidencePoints.Should().Be(4);
        Pick(sheet, g[4]).ConfidencePoints.Should().Be(3);
        Pick(sheet, g[0]).Should().BeEquivalentTo(new { ConfidencePoints = 2, Kind = AdvisedPickKind.Lean, IsCoinFlip = true });
        Pick(sheet, g[2]).Should().BeEquivalentTo(new { ConfidencePoints = 1, Kind = AdvisedPickKind.Lean, IsCoinFlip = true });

        sheet.Picks.Select(p => p.ConfidencePoints).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GoalLine_FlipsOnlyTheClosestCoinFlip_AndParksItMidSheet()
    {
        var g = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var sheet = _planner.BuildSheet(Input(AdvisorLevel.GoalLine,
            Game(g[0], 0.90), Game(g[1], 0.80), Game(g[2], 0.70), Game(g[3], 0.55), Game(g[4], 0.52)));

        sheet.FlipCount.Should().Be(1);

        var flip = Pick(sheet, g[4]);
        flip.Kind.Should().Be(AdvisedPickKind.Flip);
        flip.FranchiseSeasonId.Should().Be(Away(g[4]));
        flip.ModelProbability.Should().BeApproximately(0.48, 0.0001);
        flip.ConfidencePoints.Should().Be(3); // inserted at 5/2 = index 2 of [0.9, 0.8, ·, 0.7, 0.55]

        Pick(sheet, g[0]).ConfidencePoints.Should().Be(5);
        Pick(sheet, g[1]).ConfidencePoints.Should().Be(4);
        Pick(sheet, g[2]).ConfidencePoints.Should().Be(2);
        Pick(sheet, g[3]).Should().BeEquivalentTo(new { Kind = AdvisedPickKind.Lean, ConfidencePoints = 1 });
    }

    [Fact]
    public void QbDraw_FlipsUpToThree_LeastCertainFirst_UpperMiddlePoints()
    {
        var g = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var sheet = _planner.BuildSheet(Input(AdvisorLevel.QbDraw,
            Game(g[0], 0.90), Game(g[1], 0.59), Game(g[2], 0.58), Game(g[3], 0.55), Game(g[4], 0.52)));

        sheet.FlipCount.Should().Be(3);
        sheet.CoinFlipCount.Should().Be(4);

        Pick(sheet, g[4]).Kind.Should().Be(AdvisedPickKind.Flip);
        Pick(sheet, g[3]).Kind.Should().Be(AdvisedPickKind.Flip);
        Pick(sheet, g[2]).Kind.Should().Be(AdvisedPickKind.Flip);
        Pick(sheet, g[1]).Kind.Should().Be(AdvisedPickKind.Lean); // the 4th coin flip stays

        // Inserted at 5/4 = index 1: [0.9, f.52, f.55, f.58, 0.59]
        Pick(sheet, g[0]).ConfidencePoints.Should().Be(5);
        Pick(sheet, g[4]).ConfidencePoints.Should().Be(4);
        Pick(sheet, g[3]).ConfidencePoints.Should().Be(3);
        Pick(sheet, g[2]).ConfidencePoints.Should().Be(2);
        Pick(sheet, g[1]).ConfidencePoints.Should().Be(1);
    }

    [Fact]
    public void HailMary_FlipsEveryCoinFlip_AndGivesThemTheTopPoints()
    {
        var g = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var sheet = _planner.BuildSheet(Input(AdvisorLevel.HailMary,
            Game(g[0], 0.90), Game(g[1], 0.80), Game(g[2], 0.55), Game(g[3], 0.52)));

        sheet.FlipCount.Should().Be(2);

        Pick(sheet, g[3]).Should().BeEquivalentTo(new { Kind = AdvisedPickKind.Flip, ConfidencePoints = 4, FranchiseSeasonId = Away(g[3]) });
        Pick(sheet, g[2]).Should().BeEquivalentTo(new { Kind = AdvisedPickKind.Flip, ConfidencePoints = 3, FranchiseSeasonId = Away(g[2]) });
        Pick(sheet, g[0]).Should().BeEquivalentTo(new { Kind = AdvisedPickKind.Lock, ConfidencePoints = 2 });
        Pick(sheet, g[1]).Should().BeEquivalentTo(new { Kind = AdvisedPickKind.Lock, ConfidencePoints = 1 });
    }

    [Fact]
    public void PreviewDisagreement_IsACoinFlip_EvenWhenTheModelIsSure()
    {
        var sure = Guid.NewGuid();
        var disputed = Guid.NewGuid();
        var close = Guid.NewGuid();

        var sheet = _planner.BuildSheet(Input(AdvisorLevel.HailMary,
            Game(sure, 0.85, preview: Home(sure)),
            Game(disputed, 0.75, preview: Away(disputed)),
            Game(close, 0.53)));

        Pick(sheet, sure).Should().BeEquivalentTo(new { Kind = AdvisedPickKind.Lock, PreviewAgrees = true, IsCoinFlip = false });

        var d = Pick(sheet, disputed);
        d.IsCoinFlip.Should().BeTrue();
        d.PreviewAgrees.Should().BeFalse();
        d.Kind.Should().Be(AdvisedPickKind.Flip);
        d.FranchiseSeasonId.Should().Be(Away(disputed)); // the flip lands on the preview's side
        // Disagreement sorts as the least certain of all: it gets the top points.
        d.ConfidencePoints.Should().Be(3);
        Pick(sheet, close).ConfidencePoints.Should().Be(2);
        Pick(sheet, sure).ConfidencePoints.Should().Be(1);
    }

    [Fact]
    public void Prevent_WithPreviewDisagreement_KeepsTheModelSide_ButRanksItLast()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var sheet = _planner.BuildSheet(Input(AdvisorLevel.Prevent,
            Game(a, 0.90, preview: Away(a)),
            Game(b, 0.62)));

        Pick(sheet, a).Should().BeEquivalentTo(new { Kind = AdvisedPickKind.Lean, FranchiseSeasonId = Home(a), ConfidencePoints = 1, PreviewAgrees = false });
        Pick(sheet, b).Should().BeEquivalentTo(new { Kind = AdvisedPickKind.Lock, ConfidencePoints = 2 });
    }

    [Fact]
    public void LockedGames_AreUntouched_AndTheirPointsAreReserved()
    {
        var locked = Guid.NewGuid();
        var open1 = Guid.NewGuid();
        var open2 = Guid.NewGuid();
        var myLockedSide = Away(locked);

        var sheet = _planner.BuildSheet(Input(AdvisorLevel.HailMary,
            Game(locked, 0.51, locked: true, existing: new PickAdvisorExistingPick(myLockedSide, 3)),
            Game(open1, 0.90),
            Game(open2, 0.70)));

        sheet.LockedCount.Should().Be(1);
        sheet.FlipCount.Should().Be(0); // a locked coin flip is never a candidate
        Pick(sheet, locked).Should().BeEquivalentTo(new
        {
            Kind = AdvisedPickKind.Locked, FranchiseSeasonId = myLockedSide, ConfidencePoints = 3, DiffersFromExisting = false
        });
        Pick(sheet, open1).ConfidencePoints.Should().Be(2);
        Pick(sheet, open2).ConfidencePoints.Should().Be(1);
    }

    [Fact]
    public void NoPrediction_IsLeftBlank_AndTheLowestValueIsLeftForTheUser()
    {
        var blank = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var sheet = _planner.BuildSheet(Input(AdvisorLevel.Prevent,
            Game(blank, 0, noModel: true), Game(a, 0.80), Game(b, 0.65)));

        sheet.NoPredictionCount.Should().Be(1);
        Pick(sheet, blank).Should().BeEquivalentTo(new
        {
            Kind = AdvisedPickKind.NoPrediction, FranchiseSeasonId = (Guid?)null, ConfidencePoints = (int?)null, DiffersFromExisting = true
        });
        Pick(sheet, a).ConfidencePoints.Should().Be(3);
        Pick(sheet, b).ConfidencePoints.Should().Be(2);
    }

    [Fact]
    public void ModelNamingNeitherTeam_IsNoPrediction()
    {
        var contestId = Guid.NewGuid();
        var (home, away) = Sides(contestId);
        var stranger = Guid.NewGuid();

        var sheet = _planner.BuildSheet(Input(AdvisorLevel.Prevent,
            new PickAdvisorMatchup(contestId, null, Kickoff, false, home, away, new PickAdvisorSignal(stranger, 0.9), null, null)));

        Pick(sheet, contestId).Kind.Should().Be(AdvisedPickKind.NoPrediction);
    }

    [Fact]
    public void ProbabilityBelowHalf_NormalizesToTheOtherSide()
    {
        var contestId = Guid.NewGuid();

        var sheet = _planner.BuildSheet(Input(AdvisorLevel.Prevent, Game(contestId, 0.30)));

        Pick(sheet, contestId).Should().BeEquivalentTo(new
        {
            Kind = AdvisedPickKind.Lock, FranchiseSeasonId = Away(contestId), ModelProbability = 0.70, IsCoinFlip = false
        });
    }

    [Fact]
    public void NonConfidenceLeague_CarriesNoPoints()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var sheet = _planner.BuildSheet(new PickAdvisorPlanInput(AdvisorLevel.Prevent, UseConfidencePoints: false,
            [Game(a, 0.90), Game(b, 0.55, existing: new PickAdvisorExistingPick(Home(b), null))]));

        sheet.Picks.Should().OnlyContain(p => p.ConfidencePoints == null);
        Pick(sheet, b).DiffersFromExisting.Should().BeFalse(); // same side; points irrelevant here
    }

    [Fact]
    public void DiffersFromExisting_ReflectsSideAndPoints()
    {
        var same = Guid.NewGuid();
        var otherSide = Guid.NewGuid();
        var otherPoints = Guid.NewGuid();

        var sheet = _planner.BuildSheet(Input(AdvisorLevel.Prevent,
            Game(same, 0.90, existing: new PickAdvisorExistingPick(Home(same), 3)),
            Game(otherSide, 0.80, existing: new PickAdvisorExistingPick(Away(otherSide), 2)),
            Game(otherPoints, 0.70, existing: new PickAdvisorExistingPick(Home(otherPoints), 3))));

        Pick(sheet, same).DiffersFromExisting.Should().BeFalse();
        Pick(sheet, otherSide).DiffersFromExisting.Should().BeTrue();
        Pick(sheet, otherPoints).DiffersFromExisting.Should().BeTrue();
    }

    [Fact]
    public void Ties_BreakOnKickoff_ThenContestId_SoTheSheetIsDeterministic()
    {
        var early = Guid.NewGuid();
        var late = Guid.NewGuid();

        var sheet1 = _planner.BuildSheet(Input(AdvisorLevel.Prevent, Game(early, 0.7, kickoffOffsetHours: 0), Game(late, 0.7, kickoffOffsetHours: 3)));
        var sheet2 = _planner.BuildSheet(Input(AdvisorLevel.Prevent, Game(late, 0.7, kickoffOffsetHours: 3), Game(early, 0.7, kickoffOffsetHours: 0)));

        Pick(sheet1, early).ConfidencePoints.Should().Be(2);
        Pick(sheet2, early).ConfidencePoints.Should().Be(2);
    }

    // Horizon is weeks (known) × this week's slate (known). Unit here: 0.5
    // per game × 10 games = a 5-point typical weekly swing.
    [Theory]
    [InlineData(0, 3, 10, 0.5, AdvisorLevel.Prevent)]      // not behind
    [InlineData(-4, 3, 10, 0.5, AdvisorLevel.Prevent)]     // leading
    [InlineData(6, 3, 10, 0.5, AdvisorLevel.GoalLine)]     // 2/week = 0.4 units
    [InlineData(20, 3, 10, 0.5, AdvisorLevel.QbDraw)]      // 6.7/week = 1.3 units
    [InlineData(45, 3, 10, 0.5, AdvisorLevel.HailMary)]    // 15/week = 3 units
    [InlineData(20, 1, 10, 0.5, AdvisorLevel.HailMary)]    // same deficit, last week: 4 units
    [InlineData(15, 3, 10, 0.5, AdvisorLevel.QbDraw)]      // exactly 1.0 unit is not < 1
    [InlineData(20, 3, 20, 0.5, AdvisorLevel.GoalLine)]    // bigger slate this week → same deficit is a smaller ask
    [InlineData(30, 0, 10, 0.5, AdvisorLevel.HailMary)]    // zero weeks clamps to one
    [InlineData(6, 3, 0, 0.5, AdvisorLevel.HailMary)]      // zero games clamps to one game: 2/week = 4 units
    public void RecommendLevel_SpreadsTheDeficitOverWeeks_AgainstThisWeeksSwing(
        int deficit, int weeks, int gamesThisWeek, double stdDev, AdvisorLevel expected)
    {
        _planner.RecommendLevel(new PickAdvisorStandings(deficit, weeks, gamesThisWeek, stdDev, LeaderPointsPerGame: 5))
            .Should().Be(expected);
    }

    [Fact]
    public void RecommendLevel_FallsBackToAFractionOfTheLeaderRate_WhenNoStdDev()
    {
        // 15% of 5.0 points per game = 0.75 per game × 10 games = 7.5 per week
        _planner.RecommendLevel(new PickAdvisorStandings(5, 1, 10, null, 5)).Should().Be(AdvisorLevel.GoalLine);   // 0.67 units
        _planner.RecommendLevel(new PickAdvisorStandings(10, 1, 10, null, 5)).Should().Be(AdvisorLevel.QbDraw);    // 1.33 units
        _planner.RecommendLevel(new PickAdvisorStandings(20, 1, 10, null, 5)).Should().Be(AdvisorLevel.HailMary);  // 2.67 units
        // A league with no scored rate yet: unit floors at MinUnit (0.05) × 10 games = 0.5
        _planner.RecommendLevel(new PickAdvisorStandings(1, 1, 10, null, 0)).Should().Be(AdvisorLevel.HailMary);  // 2 units
    }
}
