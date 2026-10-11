#nullable enable
using FluentAssertions;

using SportsData.Producer.Application.Competitions.InstantAnalysis;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions.InstantAnalysis;

public class FactPhraserTests
{
    private static readonly Guid Cooley = Guid.NewGuid();
    private static readonly Guid Hughes = Guid.NewGuid();
    private static readonly Guid Team = Guid.NewGuid();

    private static readonly AnalysisNames Names = new(
        new Dictionary<Guid, string> { [Cooley] = "T. Cooley", [Hughes] = "J. Hughes" },
        new Dictionary<Guid, string> { [Team] = "Alabama" });

    [Theory]
    [InlineData(FactTypes.SeasonCount, PlayKind.Interception, 2, "T. Cooley's second interception of the season.")]
    [InlineData(FactTypes.SeasonCount, PlayKind.Sack, 3.5, "T. Cooley has 3.5 sacks this season.")]
    [InlineData(FactTypes.SeasonCount, PlayKind.Sack, 1, "T. Cooley has 1 sack this season.")]
    [InlineData(FactTypes.SeasonCount, PlayKind.FumbleLost, 1, "T. Cooley's first fumble recovery of the season.")]
    [InlineData(FactTypes.SeasonCount, PlayKind.PassingTouchdown, 12, "T. Cooley's 12th touchdown pass of the season.")]
    [InlineData(FactTypes.SeasonCount, PlayKind.ReceivingTouchdown, 5, "T. Cooley's fifth touchdown catch of the season.")]
    [InlineData(FactTypes.SeasonCount, PlayKind.RushingTouchdown, 21, "T. Cooley's 21st rushing touchdown of the season.")]
    [InlineData(FactTypes.SeasonCountAgainst, PlayKind.Interception, 1, "T. Cooley has thrown 1 interception this season.")]
    [InlineData(FactTypes.SeasonCountAgainst, PlayKind.Sack, 10, "T. Cooley has been sacked 10 times this season.")]
    [InlineData(FactTypes.SeasonCountAgainst, PlayKind.FumbleLost, 2, "T. Cooley has lost 2 fumbles this season.")]
    [InlineData(FactTypes.GameCount, PlayKind.Interception, 2, "T. Cooley's second interception of the game.")]
    [InlineData(FactTypes.GameCount, PlayKind.Sack, 1, "T. Cooley's first sack of the game.")]
    [InlineData(FactTypes.GameCount, PlayKind.FumbleLost, 1, "T. Cooley's first fumble recovery of the game.")]
    [InlineData(FactTypes.GameCount, PlayKind.PassingTouchdown, 3, "T. Cooley's third touchdown pass of the game.")]
    [InlineData(FactTypes.GameCount, PlayKind.ReceivingTouchdown, 2, "T. Cooley's second touchdown catch of the game.")]
    [InlineData(FactTypes.GameCount, PlayKind.RushingTouchdown, 1, "T. Cooley's first rushing touchdown of the game.")]
    [InlineData(FactTypes.GameCountAgainst, PlayKind.Interception, 2, "T. Cooley's second interception thrown this game.")]
    [InlineData(FactTypes.GameCountAgainst, PlayKind.Sack, 1, "T. Cooley has been sacked once this game.")]
    [InlineData(FactTypes.GameCountAgainst, PlayKind.FumbleLost, 1, "T. Cooley's first lost fumble this game.")]
    [InlineData(FactTypes.GameLongest, PlayKind.ExplosiveRun, 42, "T. Cooley's 42-yard run is the longest of the game.")]
    [InlineData(FactTypes.GameLongest, PlayKind.ExplosivePass, 61, "T. Cooley's 61-yard catch is the longest pass play of the game.")]
    public void AthleteTemplates(string factType, PlayKind kind, double value, string expected)
    {
        Render(new PlayFact(factType, kind, Cooley, null, (decimal)value, Provenance())).Should().Be(expected);
    }

    [Theory]
    [InlineData(PlayKind.Sack, "Alabama's third sack of the game.")]
    [InlineData(PlayKind.Interception, "Alabama's third takeaway of the game.")]
    [InlineData(PlayKind.FumbleLost, "Alabama's third takeaway of the game.")]
    public void TeamTemplates(PlayKind kind, string expected)
    {
        Render(new PlayFact(FactTypes.TeamGameCount, kind, null, Team, 3, Provenance())).Should().Be(expected);
    }

    [Theory]
    [InlineData("rushing.longRushing", "T. Cooley's longest run of the season (48 yards).")]
    [InlineData("passing.longPassing", "T. Cooley's longest completion of the season (48 yards).")]
    [InlineData("receiving.longReception", "T. Cooley's longest catch of the season (48 yards).")]
    public void SeasonLongest_SaysWhichLongItBeat(string stat, string expected)
    {
        Render(new PlayFact(FactTypes.SeasonLongest, PlayKind.ExplosiveRun, Cooley, null, 48, Provenance(("stat", stat))))
            .Should().Be(expected);
    }

    [Fact]
    public void NameEndingInS_TakesABareApostrophe()
    {
        Render(new PlayFact(FactTypes.GameCount, PlayKind.Sack, Hughes, null, 2, Provenance()))
            .Should().Be("J. Hughes' second sack of the game.");
    }

    [Theory]
    [InlineData(1, "first")]
    [InlineData(10, "tenth")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(22, "22nd")]
    [InlineData(23, "23rd")]
    [InlineData(101, "101st")]
    [InlineData(111, "111th")]
    public void Ordinals(int value, string expected)
    {
        FactPhraser.Ordinal(value).Should().Be(expected);
    }

    [Fact]
    public void MissingName_RendersNothing_WithAReason()
    {
        var text = FactPhraser.Render(
            new PlayFact(FactTypes.GameCount, PlayKind.Sack, Guid.NewGuid(), null, 1, Provenance()), Names, out var reason);

        text.Should().BeNull();
        reason.Should().Be("No athlete name.");
    }

    [Fact]
    public void NoTemplate_RendersNothing_WithAReason()
    {
        var text = FactPhraser.Render(
            new PlayFact(FactTypes.GameCountAgainst, PlayKind.RushingTouchdown, Cooley, null, 1, Provenance()), Names, out var reason);

        text.Should().BeNull();
        reason.Should().Be("No template for GameCountAgainst/RushingTouchdown.");
    }

    [Fact]
    public void Rendering_IsDeterministic()
    {
        var fact = new PlayFact(FactTypes.SeasonCount, PlayKind.Interception, Cooley, null, 2, Provenance());

        Enumerable.Range(0, 5).Select(_ => Render(fact)).Distinct().Should().ContainSingle();
    }

    private static string? Render(PlayFact fact) => FactPhraser.Render(fact, Names, out _);

    private static IReadOnlyDictionary<string, object?> Provenance(params (string Key, object? Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value);
}
