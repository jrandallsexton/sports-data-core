using FluentAssertions;

using SportsData.Api.Application.UI.TeamCard;
using SportsData.Core.Dtos.Canonical;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.TeamCard;

/// <summary>
/// The formatter owns two things the comparison UIs depend on: which keys
/// are lower-is-better (the favored side flips on them) and which
/// housekeeping keys never reach a client at all.
/// </summary>
public class StatFormattingServiceTests
{
    private static FranchiseSeasonStatisticDto Dto(string category, params string[] keys) => new()
    {
        Statistics = new Dictionary<string, List<FranchiseSeasonStatisticDto.FranchiseSeasonStatisticEntry>>
        {
            [category] = keys.Select(k => new FranchiseSeasonStatisticDto.FranchiseSeasonStatisticEntry
            {
                Category = category,
                StatisticKey = k,
                StatisticValue = k,
                DisplayValue = "1"
            }).ToList()
        }
    };

    [Theory]
    [InlineData("passing", "teamGamesPlayed")]
    [InlineData("passing", "miscYards")]
    [InlineData("passing", "offensiveSnapPct")]
    [InlineData("defensive", "teamGamesPlayed")]
    [InlineData("general", "gamesPlayed")]
    public void HousekeepingKeys_AreDropped(string category, string key)
    {
        var dto = Dto(category, key, "interceptions");

        new StatFormattingService().ApplyFriendlyLabelsAndFormatting(dto);

        dto.Statistics[category].Should().NotContain(e => e.StatisticKey == key);
        dto.Statistics[category].Should().ContainSingle(e => e.StatisticKey == "interceptions");
    }

    [Theory]
    [InlineData("passing", "interceptions", true)]
    [InlineData("passing", "interceptionPct", true)]
    [InlineData("passing", "passingFumblesLost", true)]
    [InlineData("passing", "netPassingYards", false)]
    [InlineData("defensive", "pointsAllowed", true)]
    [InlineData("defensive", "yardsAllowed", true)]
    [InlineData("defensive", "sacks", false)]            // a defense's sacks are good
    [InlineData("defensive", "interceptions", false)]             // a defense's picks are takeaways
    [InlineData("defensiveInterceptions", "interceptions", false)] // ESPN's own category for them
    [InlineData("kicking", "fieldGoalsBlocked", true)]
    [InlineData("miscellaneous", "totalPenaltyYards", true)]
    [InlineData("miscellaneous", "totalTakeaways", false)]
    [InlineData("miscellaneous", "fumblesLost", true)]
    [InlineData("punting", "puntsBlockedPct", true)]
    [InlineData("punting", "punts", true)]              // fewer punts = fewer failed drives
    [InlineData("punting", "avgPuntReturnYards", true)] // what opponents did with them
    [InlineData("punting", "netAvgPuntYards", false)]
    [InlineData("punting", "puntsInside20", false)]
    [InlineData("receiving", "receivingFumblesLost", true)]
    [InlineData("returning", "puntReturnFumblesLost", true)]
    [InlineData("returning", "fumbleRecoveries", false)]
    public void Polarity_IsFlaggedPerCategory(string category, string key, bool lowerIsBetter)
    {
        var dto = Dto(category, key);

        new StatFormattingService().ApplyFriendlyLabelsAndFormatting(dto);

        dto.Statistics[category].Single().IsNegativeAttribute.Should().Be(lowerIsBetter);
    }

    [Fact]
    public void DefensiveInterceptions_IsARegisteredCategory_SoItsRowsGetLabels()
    {
        var dto = Dto("defensiveInterceptions", "interceptionTouchdowns");

        new StatFormattingService().ApplyFriendlyLabelsAndFormatting(dto);

        // An unregistered category is skipped entirely and reaches clients with the
        // raw key as its label; this one must not.
        dto.Statistics["defensiveInterceptions"].Single().StatisticValue.Should().Be("Pick 6");
    }
}
