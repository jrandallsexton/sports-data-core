#nullable enable
using FluentAssertions;

using SportsData.Producer.Application.Competitions.InstantAnalysis;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions.InstantAnalysis;

public class FactSelectorTests
{
    private static readonly Guid Offense = Guid.NewGuid();
    private static readonly Guid Defense = Guid.NewGuid();
    private static readonly Guid Passer = Guid.NewGuid();
    private static readonly Guid Defender = Guid.NewGuid();

    private static readonly DateTime PreviousFinal = new(2026, 9, 27, 3, 0, 0, DateTimeKind.Utc);

    private static readonly AnalysisNames Names = new(
        new Dictionary<Guid, string> { [Passer] = "J. Smith", [Defender] = "T. Cooley" },
        new Dictionary<Guid, string> { [Defense] = "Alabama" });

    [Fact]
    public void AllFactsAreKept_AndOrderedByPriority()
    {
        var facts = new[]
        {
            Fact(FactTypes.GameCountAgainst, PlayKind.Interception, Passer, 1),
            new PlayFact(FactTypes.TeamGameCount, PlayKind.Interception, null, Defense, 2, Empty),
            Fact(FactTypes.GameCount, PlayKind.Interception, Defender, 1),
            Season(FactTypes.SeasonCount, PlayKind.Interception, Defender, 2, PreviousFinal.AddHours(5)),
            Season(FactTypes.SeasonCountAgainst, PlayKind.Interception, Passer, 6, PreviousFinal.AddHours(5))
        };

        var result = FactSelector.Select(facts, Teams(), Names);

        result.Dropped.Should().BeEmpty();
        result.Selected.Select(s => s.Fact.FactType).Should().Equal(
            FactTypes.SeasonCount,
            FactTypes.GameCount,
            FactTypes.TeamGameCount,
            FactTypes.SeasonCountAgainst,
            FactTypes.GameCountAgainst);
        result.Selected.Select(s => s.Ordinal).Should().Equal(0, 1, 2, 3, 4);
        result.Selected.Should().OnlyContain(s => s.TemplateVersion == FactPhraser.TemplateVersion);
        result.Selected[0].Text.Should().Be("T. Cooley's second interception of the season.");
    }

    [Fact]
    public void SeasonDocumentWrittenBeforeThePreviousGameFinalized_IsDroppedAsStale()
    {
        var stale = Season(FactTypes.SeasonCount, PlayKind.Interception, Defender, 2, PreviousFinal.AddHours(-1));

        var result = FactSelector.Select([stale], Teams(), Names);

        result.Selected.Should().BeEmpty();
        result.Dropped.Should().ContainSingle(d =>
            d.FactType == FactTypes.SeasonCount && d.AthleteSeasonId == Defender && d.Reason.StartsWith("Season document"));
    }

    [Fact]
    public void Freshness_IsCheckedAgainstTheAthletesOwnSide()
    {
        // The defense's previous game finalized late; the offense's long ago.
        var teams = new TeamFreshness(Offense, Defense, new Dictionary<Guid, DateTime?>
        {
            [Offense] = PreviousFinal.AddDays(-1),
            [Defense] = PreviousFinal
        });
        var written = PreviousFinal.AddHours(-1);

        var result = FactSelector.Select(
        [
            Season(FactTypes.SeasonCount, PlayKind.Interception, Defender, 2, written),     // defense: stale
            Season(FactTypes.SeasonCountAgainst, PlayKind.Interception, Passer, 6, written) // offense: fresh
        ], teams, Names);

        result.Selected.Should().ContainSingle(s => s.Fact.AthleteSeasonId == Passer);
        result.Dropped.Should().ContainSingle(d => d.AthleteSeasonId == Defender);
    }

    [Fact]
    public void PreviousGameNotFinalized_DropsSeasonFacts()
    {
        var teams = new TeamFreshness(Offense, Defense, new Dictionary<Guid, DateTime?> { [Defense] = null });

        var result = FactSelector.Select(
            [Season(FactTypes.SeasonCount, PlayKind.Interception, Defender, 2, PreviousFinal)], teams, Names);

        result.Dropped.Should().ContainSingle(d => d.Reason.Contains("isn't finalized"));
    }

    [Fact]
    public void FirstGameOfTheSeason_HasNothingToBeStaleAgainst()
    {
        var teams = new TeamFreshness(Offense, Defense, new Dictionary<Guid, DateTime?>());

        var result = FactSelector.Select(
            [Season(FactTypes.SeasonCount, PlayKind.Interception, Defender, 1, PreviousFinal)], teams, Names);

        result.Selected.Should().ContainSingle();
    }

    [Fact]
    public void GameFacts_AreNeverStale()
    {
        var result = FactSelector.Select(
            [Fact(FactTypes.GameCount, PlayKind.Interception, Defender, 1)],
            new TeamFreshness(null, null, new Dictionary<Guid, DateTime?>()),
            Names);

        result.Selected.Should().ContainSingle();
    }

    [Fact]
    public void UnphraseableFact_IsDropped_WithTheReason()
    {
        var result = FactSelector.Select(
            [Fact(FactTypes.GameCount, PlayKind.Interception, Guid.NewGuid(), 1)], Teams(), Names);

        result.Selected.Should().BeEmpty();
        result.Dropped.Should().ContainSingle(d => d.Reason == "No athlete name.");
    }

    [Fact]
    public void Selection_IsDeterministic_RegardlessOfInputOrder()
    {
        var facts = new[]
        {
            Fact(FactTypes.GameCount, PlayKind.PassingTouchdown, Passer, 2),
            Fact(FactTypes.GameCount, PlayKind.Interception, Defender, 1),
            Fact(FactTypes.GameCount, PlayKind.Sack, Defender, 1)
        };

        var forward = FactSelector.Select(facts, Teams(), Names).Selected.Select(s => s.Text);
        var reversed = FactSelector.Select(facts.Reverse(), Teams(), Names).Selected.Select(s => s.Text);

        forward.Should().Equal(reversed);
    }

    private static TeamFreshness Teams() =>
        new(Offense, Defense, new Dictionary<Guid, DateTime?> { [Offense] = PreviousFinal, [Defense] = PreviousFinal });

    private static readonly IReadOnlyDictionary<string, object?> Empty = new Dictionary<string, object?>();

    private static PlayFact Fact(string factType, PlayKind kind, Guid athlete, decimal value) =>
        new(factType, kind, athlete, null, value, Empty);

    private static PlayFact Season(string factType, PlayKind kind, Guid athlete, decimal value, DateTime documentUtc) =>
        new(factType, kind, athlete, null, value, Empty) { SeasonDocumentCreatedUtc = documentUtc };
}
