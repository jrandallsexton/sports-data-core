using FluentAssertions;

using SportsData.Api.Application.UI.PlayerLineups.Scoring;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.PlayerLineups;

/// <summary>
/// The scoring engine: per-unit math, derived kicker keys, and the
/// matrix-is-data contract (values come from rules; structure from
/// code). Rules here mirror the seeded Standard set where relevant.
/// </summary>
public class PlayerPickemScoringEngineTests
{
    private static ScoringRule Rule(string key, decimal points, decimal perUnits = 1m) =>
        new(key, points, perUnits);

    [Fact]
    public void QbLine_ScoresFractionalYards_AndCountsNegatives()
    {
        // 187 pass yds @ 1/25 = 7.48; 2 pass TD = 12; 1 INT = -2 → 17.48
        var rules = new[]
        {
            Rule("passing.passingYards", 1m, 25m),
            Rule("passing.passingTouchdowns", 6m),
            Rule("passing.interceptions", -2m),
        };
        var stats = new Dictionary<string, decimal>
        {
            ["passing.passingYards"] = 187m,
            ["passing.passingTouchdowns"] = 2m,
            ["passing.interceptions"] = 1m,
        };

        var score = PlayerPickemScoringEngine.Score(rules, stats);

        score.Points.Should().Be(17.48m);
        score.Contributions.Should().HaveCount(3);
    }

    [Fact]
    public void KickerBuckets_DeriveFromRawStats()
    {
        // 2 XP made of 3 attempts (-2 for the miss), one 33-yarder made
        // (3 pts via the 17-39 tier), one 45-yarder missed (-1).
        var rules = new[]
        {
            Rule("kicking.extraPointsMade", 1m),
            Rule("derived.missedExtraPoints", -2m),
            Rule("derived.fieldGoalsMade17_39", 3m),
            Rule("derived.fieldGoalsMissed40_49", -1m),
        };
        var stats = new Dictionary<string, decimal>
        {
            ["kicking.extraPointsMade"] = 2m,
            ["kicking.extraPointAttempts"] = 3m,
            ["kicking.fieldGoalsMade30_39"] = 1m,
            ["kicking.fieldGoalAttempts30_39"] = 1m,
            ["kicking.fieldGoalAttempts40_49"] = 1m,
            ["kicking.fieldGoalsMade40_49"] = 0m,
        };

        var score = PlayerPickemScoringEngine.Score(rules, stats);

        // 2 XP + (-2 miss) + 3 FG + (-1 missed 40-49) = 2
        score.Points.Should().Be(2m);
    }

    [Fact]
    public void UnscoredStats_AndZeroValues_ContributeNothing()
    {
        var rules = new[] { Rule("rushing.rushingYards", 1m, 10m) };
        var stats = new Dictionary<string, decimal>
        {
            ["rushing.rushingYards"] = 0m,
            ["receiving.receivingYards"] = 85m, // no rule for it in this set
        };

        var score = PlayerPickemScoringEngine.Score(rules, stats);

        score.Points.Should().Be(0m);
        score.Contributions.Should().BeEmpty();
    }

    [Fact]
    public void ZeroPerUnitsRule_IsIgnored_NeverDivides()
    {
        var rules = new[] { Rule("rushing.rushingYards", 1m, 0m) };
        var stats = new Dictionary<string, decimal> { ["rushing.rushingYards"] = 50m };

        var score = PlayerPickemScoringEngine.Score(rules, stats);

        score.Points.Should().Be(0m);
    }

    [Fact]
    public void StatLine_MatchesScoredContributions()
    {
        var rules = new[]
        {
            Rule("passing.passingYards", 1m, 25m),
            Rule("passing.passingTouchdowns", 6m),
        };
        var stats = new Dictionary<string, decimal>
        {
            ["passing.passingYards"] = 187m,
            ["passing.passingTouchdowns"] = 2m,
        };

        var score = PlayerPickemScoringEngine.Score(rules, stats);
        var line = PlayerPickemScoringEngine.BuildStatLine(score.Contributions);

        line.Should().Be("187 PaYd · 2 PaTD");
    }
}
