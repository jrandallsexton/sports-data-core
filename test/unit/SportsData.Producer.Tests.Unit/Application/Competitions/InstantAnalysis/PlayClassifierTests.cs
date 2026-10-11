#nullable enable
using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Producer.Application.Competitions.InstantAnalysis;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions.InstantAnalysis;

/// <summary>
/// Role sequences mirror the most common 2026 sequences found in the
/// instant-analysis data spike (docs/features/instant-analysis/01-data-spike-findings.md).
/// </summary>
public class PlayClassifierTests
{
    private static readonly Guid Passer = Guid.NewGuid();
    private static readonly Guid Receiver = Guid.NewGuid();
    private static readonly Guid Rusher = Guid.NewGuid();
    private static readonly Guid Defender = Guid.NewGuid();
    private static readonly Guid Defender2 = Guid.NewGuid();
    private static readonly Guid Tackler = Guid.NewGuid();

    [Theory]
    [InlineData("26")] // PassInterceptionReturn
    [InlineData("36")] // InterceptionReturnTouchdown
    public void Interception_ActorIsPassDefender_VictimIsPasser(string typeId)
    {
        var play = Play(typeId, 21, ("passer", Passer), ("passDefender", Defender), ("returner", Defender), ("tackler", Tackler));

        var events = PlayClassifier.Classify(play);

        events.Should().ContainSingle();
        events[0].Kind.Should().Be(PlayKind.Interception);
        events[0].Actors.Should().Equal(Defender);
        events[0].Victim.Should().Be(Passer);
    }

    [Fact]
    public void PlainInterception_NamesNoActor_ButKeepsTheVictim()
    {
        // Type 63 names only the passer; the interceptor is only in the text.
        var events = PlayClassifier.Classify(Play("63", 0, ("passer", Passer)));

        events.Should().ContainSingle();
        events[0].Kind.Should().Be(PlayKind.Interception);
        events[0].Actors.Should().BeEmpty();
        events[0].Victim.Should().Be(Passer);
    }

    [Fact]
    public void SplitSack_CreditsBothSackers()
    {
        var events = PlayClassifier.Classify(Play("7", -8, ("passer", Passer), ("sackedBy", Defender), ("sackedBy", Defender2)));

        events.Should().ContainSingle();
        events[0].Kind.Should().Be(PlayKind.Sack);
        events[0].Actors.Should().BeEquivalentTo([Defender, Defender2]);
        events[0].Victim.Should().Be(Passer);
    }

    [Fact]
    public void FumbleLost_ActorIsRecoverer_VictimIsFumbler()
    {
        var events = PlayClassifier.Classify(Play("29", 3, ("rusher", Rusher), ("recoverer", Defender), ("fumbler", Rusher), ("forcedBy", Defender2)));

        events.Should().ContainSingle();
        events[0].Kind.Should().Be(PlayKind.FumbleLost);
        events[0].Actors.Should().Equal(Defender);
        events[0].Victim.Should().Be(Rusher);
    }

    [Fact]
    public void LongPassingTouchdown_IsPassingReceivingAndExplosive()
    {
        var events = PlayClassifier.Classify(Play("67", 60, ("passer", Passer), ("receiver", Receiver), ("scorer", Receiver), ("patScorer", Guid.NewGuid())));

        events.Select(e => e.Kind).Should().BeEquivalentTo(
            [PlayKind.PassingTouchdown, PlayKind.ReceivingTouchdown, PlayKind.ExplosivePass]);
        events.Single(e => e.Kind == PlayKind.PassingTouchdown).Actors.Should().Equal(Passer);
        events.Single(e => e.Kind == PlayKind.ReceivingTouchdown).Actors.Should().Equal(Receiver);
        events.Single(e => e.Kind == PlayKind.ExplosivePass).Actors.Should().BeEquivalentTo([Passer, Receiver]);
    }

    [Fact]
    public void ShortRushingTouchdown_IsNotExplosive()
    {
        var events = PlayClassifier.Classify(Play("68", 2, ("rusher", Rusher), ("scorer", Rusher), ("patScorer", Guid.NewGuid())));

        events.Select(e => e.Kind).Should().Equal(PlayKind.RushingTouchdown);
    }

    [Theory]
    [InlineData(PlayClassifier.ExplosiveRunYards - 1, false)]
    [InlineData(PlayClassifier.ExplosiveRunYards, true)]
    public void Rush_IsExplosiveFromTheThreshold(int yards, bool explosive)
    {
        var events = PlayClassifier.Classify(Play("5", yards, ("rusher", Rusher), ("tackler", Tackler)));

        if (explosive)
            events.Should().ContainSingle(e => e.Kind == PlayKind.ExplosiveRun && e.Actors.Single() == Rusher);
        else
            events.Should().BeEmpty();
    }

    [Theory]
    [InlineData(PlayClassifier.ExplosivePassYards - 1, false)]
    [InlineData(PlayClassifier.ExplosivePassYards, true)]
    public void Reception_IsExplosiveFromTheThreshold(int yards, bool explosive)
    {
        var events = PlayClassifier.Classify(Play("24", yards, ("passer", Passer), ("receiver", Receiver), ("tackler", Tackler)));

        if (explosive)
            events.Should().ContainSingle(e => e.Kind == PlayKind.ExplosivePass);
        else
            events.Should().BeEmpty();
    }

    [Fact]
    public void ExplosiveRun_WithNoNamedRusher_SaysNothing()
    {
        PlayClassifier.Classify(Play("5", 40, ("tackler", Tackler))).Should().BeEmpty();
    }

    [Fact]
    public void ParticipantWithUnresolvedAthlete_IsNotCredited()
    {
        var play = Play("26", 0, ("passer", Passer)) with
        {
            Participants = [new AnalysedPlayParticipant(Passer, "passer", 0), new AnalysedPlayParticipant(null, "passDefender", 1)]
        };

        PlayClassifier.Classify(play).Single().Actors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("20")]  // Safety: roles inconsistent, out of Phase 1
    [InlineData("59")]  // Field goal: not a Phase 1 kind
    [InlineData("999")] // Unknown to the enum
    [InlineData("abc")] // Not a number
    public void OutOfScopeOrUnknownTypes_ClassifyToNothing(string typeId)
    {
        PlayClassifier.Classify(Play(typeId, 30, ("rusher", Rusher), ("kicker", Rusher))).Should().BeEmpty();
    }

    private static AnalysedPlay Play(string typeId, int yards, params (string Role, Guid Athlete)[] participants) =>
        new(
            PlayId: Guid.NewGuid(),
            ContestId: Guid.NewGuid(),
            CompetitionId: Guid.NewGuid(),
            SequenceNumber: "100",
            Type: PlayType.Unknown,
            TypeId: typeId,
            Text: "play",
            StatYardage: yards,
            StartFranchiseSeasonId: null,
            Participants: participants
                .Select((p, i) => new AnalysedPlayParticipant(p.Athlete, p.Role, i))
                .ToList());
}
