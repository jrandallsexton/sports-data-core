using FluentAssertions;

using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonPreviewStats;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseSeasons.Queries.GetFranchiseSeasonPreviewStats;

/// <summary>
/// The rows below are Indiana 2026 after week 3 as stored in prod (local copy,
/// 2026-09-24): PerGameValue null on every one of them, ESPN's own per-game
/// keys present, and two names repeated across categories with different
/// meanings. The old mapper produced all zeros from exactly this input.
/// </summary>
public class FranchiseSeasonModelStatsMapperTests
{
    private static FranchiseSeasonRawStat Row(string category, string statistic, double? value, double? perGame = null) =>
        new() { Category = category, Statistic = statistic, Value = value, PerGameValue = perGame };

    private static readonly FranchiseSeasonRawStat[] Indiana =
    [
        Row("scoring", "totalPointsPerGame", 48.333),
        Row("passing", "totalPointsPerGame", 48.33),
        Row("passing", "yardsPerGame", 461),
        Row("rushing", "yardsPerGame", 461),
        Row("passing", "passingYardsPerGame", 240),
        Row("passing", "passingYards", 720),
        Row("rushing", "rushingYardsPerGame", 221),
        Row("rushing", "rushingYards", 663),
        Row("miscellaneous", "thirdDownConvPct", 48.27586),
        Row("miscellaneous", "redzoneScoringPct", 0),
        Row("miscellaneous", "turnOverDifferential", 1),
        Row("general", "totalPenalties", 9),
        Row("miscellaneous", "totalPenalties", 9),
        Row("general", "totalPenaltyYards", 78),
        Row("passing", "totalOffensivePlays", 178),
        Row("passing", "totalYardsFromScrimmage", 1383),
        Row("rushing", "totalYardsFromScrimmage", 1383),
        Row("defensive", "sacks", 7),          // sacks made
        Row("passing", "sacks", 4),            // sacks taken - must NOT be picked
        Row("defensiveInterceptions", "interceptions", 3), // picks made
        Row("passing", "interceptions", 0),    // thrown - must NOT be picked
        Row("general", "fumblesLost", 0),
        Row("miscellaneous", "totalTakeaways", 2),
        Row("defensive", "teamGamesPlayed", 3),
        Row("passing", "teamGamesPlayed", 3),
        // The only rows ESPN gives a per-game value for; none are mapped.
        Row("kicking", "fieldGoalPct", 100, perGame: 100),
        Row("returning", "avgKickoffReturnYards", 21.5, perGame: 21.5),
    ];

    [Fact]
    public void Map_ReadsValues_NotThePerGameColumn_AndProducesTheRealNumbers()
    {
        var dto = FranchiseSeasonModelStatsMapper.Map(Indiana);

        dto.PointsPerGame.Should().BeApproximately(48.333, 0.001, "scoring.totalPointsPerGame, the category with the unrounded value");
        dto.YardsPerGame.Should().Be(461);
        dto.PassingYardsPerGame.Should().Be(240);
        dto.RushingYardsPerGame.Should().Be(221);
        dto.ThirdDownConvPct.Should().BeApproximately(48.276, 0.001);
        dto.RedZoneScoringPct.Should().Be(0, "ESPN genuinely reports 0 here; 0 is a value, not an absence");
        dto.TurnoverDifferential.Should().Be(1);

        dto.PenaltiesPerGame.Should().Be(3);
        dto.PenaltyYardsPerGame.Should().Be(26);
        dto.AvgYardsPerPlay.Should().BeApproximately(1383.0 / 178, 0.001);

        dto.Sacks.Should().Be(7, "defensive sacks, not the 4 sacks the offense took");
        dto.Interceptions.Should().Be(3, "interceptions made, not the 0 thrown");
        dto.FumblesLost.Should().Be(0);
        dto.Takeaways.Should().Be(2);
    }

    [Fact]
    public void Map_FallsBackToSeasonTotalsOverGames_WhenEspnPerGameKeysAreAbsent()
    {
        FranchiseSeasonRawStat[] rows =
        [
            Row("passing", "passingYards", 720),
            Row("rushing", "rushingYards", 663),
            Row("passing", "totalYardsFromScrimmage", 1383),
            Row("passing", "teamGamesPlayed", 3),
        ];

        var dto = FranchiseSeasonModelStatsMapper.Map(rows);

        dto.PassingYardsPerGame.Should().Be(240);
        dto.RushingYardsPerGame.Should().Be(221);
        dto.YardsPerGame.Should().Be(461);
    }

    [Fact]
    public void Map_LeavesMissingStatsNull_SoHasStatsMeansSomething()
    {
        // Weeks 1-2 shape: a few rows, no rushing yet. The caller's hasStats
        // keys on RushingYardsPerGame being non-null; a 0 here selected the
        // with-stats prompt on empty data for a whole season.
        FranchiseSeasonRawStat[] rows =
        [
            Row("scoring", "totalPointsPerGame", 0),
            Row("passing", "teamGamesPlayed", 0),
        ];

        var dto = FranchiseSeasonModelStatsMapper.Map(rows);

        dto.RushingYardsPerGame.Should().BeNull();
        dto.PassingYardsPerGame.Should().BeNull();
        dto.YardsPerGame.Should().BeNull();
        dto.Sacks.Should().BeNull();
        dto.PenaltiesPerGame.Should().BeNull("division by zero games is null, not infinity or zero");
    }

    [Fact]
    public void Map_EmptyInput_IsAllNull()
    {
        var dto = FranchiseSeasonModelStatsMapper.Map([]);

        dto.RushingYardsPerGame.Should().BeNull();
        dto.PointsPerGame.Should().BeNull();
        dto.Takeaways.Should().BeNull();
    }
}
