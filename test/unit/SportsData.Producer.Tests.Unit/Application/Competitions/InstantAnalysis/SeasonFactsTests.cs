#nullable enable
using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Producer.Application.Competitions.InstantAnalysis;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions.InstantAnalysis;

public class SeasonFactsTests
{
    private static readonly Guid Home = Guid.NewGuid();
    private static readonly Guid Away = Guid.NewGuid();
    private static readonly Guid AwayPasser = Guid.NewGuid();
    private static readonly Guid AwayReceiver = Guid.NewGuid();
    private static readonly Guid HomeDefender = Guid.NewGuid();
    private static readonly Guid HomeDefender2 = Guid.NewGuid();
    private static readonly Guid HomeRusher = Guid.NewGuid();

    private static readonly DateTime Kickoff = new(2026, 10, 3, 19, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void ExampleInterception_OneEarlierInTheSeasonPlusThisOne_IsTheSecond()
    {
        // Task 01's worked example: one interception before this game, then this one.
        var game = new List<GamePlay> { Int(30, Away, HomeDefender) };
        var stats = Stats(Doc(HomeDefender, ("defensiveInterceptions.interceptions", 1)));

        var fact = Fact(Compute(game, 30, stats), FactTypes.SeasonCount, HomeDefender);

        fact.Value.Should().Be(2);
        fact.Provenance["seasonValue"].Should().Be(1m);
        fact.Provenance["gameValue"].Should().Be(1m);
        fact.Provenance["documentRule"].Should().Be("newest");
    }

    [Fact]
    public void SeasonCount_AddsEarlierPlaysInThisGame_AndNotLaterOnes()
    {
        var game = new List<GamePlay>
        {
            Int(10, Away, HomeDefender),
            Int(30, Away, HomeDefender),
            Int(40, Away, HomeDefender)
        };
        var stats = Stats(Doc(HomeDefender, ("defensiveInterceptions.interceptions", 3)));

        Fact(Compute(game, 30, stats), FactTypes.SeasonCount, HomeDefender).Value.Should().Be(5);
    }

    [Fact]
    public void Victim_CountsInterceptionsThrown_IncludingUnattributedOnes()
    {
        var game = new List<GamePlay>
        {
            Play(10, "63", Away, 0, ("passer", AwayPasser)),
            Int(30, Away, HomeDefender)
        };
        var stats = Stats(
            Doc(AwayPasser, ("passing.interceptions", 4)),
            Doc(HomeDefender, ("defensiveInterceptions.interceptions", 1)));

        var result = Compute(game, 30, stats);

        Fact(result, FactTypes.SeasonCountAgainst, AwayPasser).Value.Should().Be(6);
        // The earlier type 63 hides who made it, so the defender's count is dropped.
        result.Facts.Should().NotContain(f => f.FactType == FactTypes.SeasonCount);
        result.Dropped.Should().Contain(d => d.FactType == FactTypes.SeasonCount && d.AthleteSeasonId == HomeDefender);
    }

    [Fact]
    public void SplitSack_CreditsAHalfToEachSacker_LikeEspn()
    {
        var game = new List<GamePlay>
        {
            Play(30, "7", Away, -7, ("passer", AwayPasser), ("sackedBy", HomeDefender), ("sackedBy", HomeDefender2))
        };
        var stats = Stats(
            Doc(HomeDefender, ("defensive.sacks", 2.5m)),
            Doc(HomeDefender2, ("defensive.sacks", 0)),
            Doc(AwayPasser, ("passing.sacks", 9)));

        var result = Compute(game, 30, stats);

        Fact(result, FactTypes.SeasonCount, HomeDefender).Value.Should().Be(3m);
        Fact(result, FactTypes.SeasonCount, HomeDefender2).Value.Should().Be(0.5m);
        Fact(result, FactTypes.SeasonCountAgainst, AwayPasser).Value.Should().Be(10);
    }

    [Fact]
    public void Touchdowns_UseEachRolesOwnSeasonStat()
    {
        var game = new List<GamePlay>
        {
            Play(30, "67", Away, 8, ("passer", AwayPasser), ("receiver", AwayReceiver), ("scorer", AwayReceiver))
        };
        var stats = Stats(
            Doc(AwayPasser, ("passing.passingTouchdowns", 11)),
            Doc(AwayReceiver, ("receiving.receivingTouchdowns", 4)));

        var result = Compute(game, 30, stats);

        Fact(result, FactTypes.SeasonCount, AwayPasser).Value.Should().Be(12);
        Fact(result, FactTypes.SeasonCount, AwayReceiver).Value.Should().Be(5);
    }

    [Theory]
    [InlineData(40, true)]   // beats the season's 30 and today's 35
    [InlineData(35, false)]  // ties today's earlier run
    [InlineData(32, false)]  // beats the season document, not today's earlier run
    public void SeasonLongest_MustBeatTheDocumentAndEarlierRunsToday(int yards, bool longest)
    {
        var game = new List<GamePlay>
        {
            Rush(10, Home, HomeRusher, 35),
            Rush(30, Home, HomeRusher, yards)
        };
        var stats = Stats(Doc(HomeRusher, ("rushing.longRushing", 30)));

        var result = Compute(game, 30, stats);

        if (longest)
        {
            var fact = Fact(result, FactTypes.SeasonLongest, HomeRusher);
            fact.Value.Should().Be(yards);
            fact.Provenance["seasonValue"].Should().Be(30m);
            fact.Provenance["previousGameLongest"].Should().Be(35);
        }
        else
        {
            result.Facts.Should().NotContain(f => f.FactType == FactTypes.SeasonLongest);
        }
    }

    [Fact]
    public void SeasonLongest_OnAnExplosivePass_ChecksPasserAndReceiverSeparately()
    {
        var game = new List<GamePlay>
        {
            Play(30, "24", Away, 45, ("passer", AwayPasser), ("receiver", AwayReceiver))
        };
        var stats = Stats(
            Doc(AwayPasser, ("passing.longPassing", 60)),
            Doc(AwayReceiver, ("receiving.longReception", 22)));

        var result = Compute(game, 30, stats);

        result.Facts.Should().NotContain(f => f.FactType == FactTypes.SeasonLongest && f.AthleteSeasonId == AwayPasser);
        Fact(result, FactTypes.SeasonLongest, AwayReceiver).Value.Should().Be(45);
    }

    [Fact]
    public void NoSeasonDocument_DropsTheFact_WithTheReason()
    {
        var game = new List<GamePlay> { Int(30, Away, HomeDefender) };

        var live = Compute(game, 30, Stats());
        var lab = Compute(game, 30, Stats(), documentCutoffUtc: Kickoff);

        live.Facts.Should().BeEmpty();
        live.Dropped.Should().Contain(d => d.AthleteSeasonId == HomeDefender && d.Reason == "No season document.");
        lab.Dropped.Should().Contain(d => d.AthleteSeasonId == HomeDefender && d.Reason == "No season document written before kickoff.");
    }

    [Fact]
    public void DocumentWithoutTheStat_DropsTheFact()
    {
        var game = new List<GamePlay> { Int(30, Away, HomeDefender) };
        var stats = Stats(Doc(HomeDefender, ("defensive.sacks", 1)));

        var result = Compute(game, 30, stats);

        result.Dropped.Should().Contain(d => d.FactType == FactTypes.SeasonCount && d.Reason.Contains("defensiveInterceptions.interceptions"));
    }

    [Fact]
    public void Provenance_FlagsALiveDocumentWrittenAfterKickoff()
    {
        var game = new List<GamePlay> { Int(30, Away, HomeDefender) };
        var stats = Stats(Doc(HomeDefender, Kickoff.AddMinutes(40), ("defensiveInterceptions.interceptions", 1)));

        var fact = Fact(Compute(game, 30, stats), FactTypes.SeasonCount, HomeDefender);

        fact.Provenance["writtenAfterKickoff"].Should().Be(true);
    }

    [Fact]
    public void NewestPerAthlete_Live_TakesTheNewestDocument()
    {
        var older = Doc(HomeDefender, Kickoff.AddDays(-6), ("defensive.sacks", 1));
        var newer = Doc(HomeDefender, Kickoff.AddHours(4), ("defensive.sacks", 2));

        SeasonFacts.NewestPerAthlete([older, newer], cutoffUtc: null)[HomeDefender].Should().Be(newer);
    }

    [Fact]
    public void NewestPerAthlete_Lab_TakesOnlyADocumentWrittenBeforeKickoff()
    {
        var before = Doc(HomeDefender, Kickoff.AddDays(-6), ("defensive.sacks", 1));
        var after = Doc(HomeDefender, Kickoff.AddHours(4), ("defensive.sacks", 2));
        var atKickoff = Doc(HomeDefender2, Kickoff, ("defensive.sacks", 2));

        var picked = SeasonFacts.NewestPerAthlete([before, after, atKickoff], cutoffUtc: Kickoff);

        picked[HomeDefender].Should().Be(before);
        picked.Should().NotContainKey(HomeDefender2);
    }

    private static FactResult Compute(
        List<GamePlay> game,
        long sequence,
        IReadOnlyDictionary<Guid, SeasonStatLine> stats,
        DateTime? documentCutoffUtc = null)
    {
        var current = Current(game.Single(p => p.Sequence == sequence));
        return SeasonFacts.Compute(current, PlayClassifier.Classify(current), game, stats, Kickoff, documentCutoffUtc);
    }

    private static PlayFact Fact(FactResult result, string factType, Guid athlete) =>
        result.Facts.Single(f => f.FactType == factType && f.AthleteSeasonId == athlete);

    private static IReadOnlyDictionary<Guid, SeasonStatLine> Stats(params SeasonStatLine[] lines) =>
        lines.ToDictionary(l => l.AthleteSeasonId);

    private static SeasonStatLine Doc(Guid athlete, params (string Key, decimal Value)[] values) =>
        Doc(athlete, Kickoff.AddDays(-2), values);

    private static SeasonStatLine Doc(Guid athlete, DateTime createdUtc, params (string Key, decimal Value)[] values) =>
        new(athlete, Guid.NewGuid(), createdUtc, values.ToDictionary(v => v.Key, v => v.Value));

    private static AnalysedPlay Current(GamePlay p) =>
        new(p.PlayId, Guid.NewGuid(), Guid.NewGuid(), p.Sequence.ToString(), PlayType.Unknown, p.TypeId,
            "play", p.StatYardage, p.OffenseFranchiseSeasonId, p.Participants);

    private static GamePlay Int(long sequence, Guid offense, Guid defender) =>
        Play(sequence, "26", offense, 10, ("passer", AwayPasser), ("passDefender", defender), ("returner", defender));

    private static GamePlay Rush(long sequence, Guid offense, Guid rusher, int yards) =>
        Play(sequence, "5", offense, yards, ("rusher", rusher));

    private static GamePlay Play(long sequence, string typeId, Guid offense, int yards, params (string Role, Guid Athlete)[] participants) =>
        new(Guid.NewGuid(), sequence, typeId, yards, offense,
            participants.Select((p, i) => new AnalysedPlayParticipant(p.Athlete, p.Role, i)).ToList());
}
