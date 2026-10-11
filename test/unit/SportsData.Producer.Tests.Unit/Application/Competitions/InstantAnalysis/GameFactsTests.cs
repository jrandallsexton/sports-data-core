#nullable enable
using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Producer.Application.Competitions.InstantAnalysis;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions.InstantAnalysis;

public class GameFactsTests
{
    private static readonly Guid Home = Guid.NewGuid();
    private static readonly Guid Away = Guid.NewGuid();
    private static readonly Guid AwayPasser = Guid.NewGuid();
    private static readonly Guid HomeDefender = Guid.NewGuid();
    private static readonly Guid HomeDefender2 = Guid.NewGuid();
    private static readonly Guid HomeRusher = Guid.NewGuid();

    private static readonly IReadOnlyDictionary<Guid, Guid> DefenseByOffense =
        new Dictionary<Guid, Guid> { [Home] = Away, [Away] = Home };

    [Fact]
    public void SecondInterceptionToday_CountsTheEarlierOne_AndNotLaterPlays()
    {
        var game = new List<GamePlay>
        {
            Int(10, Away, HomeDefender),
            Rush(20, Home, HomeRusher, 4),
            Int(30, Away, HomeDefender),     // the play
            Int(40, Away, HomeDefender)      // later: must not count
        };

        var result = Compute(game, sequence: 30);

        Fact(result, FactTypes.GameCount, HomeDefender).Value.Should().Be(2);
        Fact(result, FactTypes.GameCountAgainst, AwayPasser).Value.Should().Be(2);
    }

    [Fact]
    public void EarlierUnattributedInterception_DropsTheActorCount_ButKeepsVictimAndTeam()
    {
        var game = new List<GamePlay>
        {
            Play(10, "63", Away, 0, ("passer", AwayPasser)),
            Int(30, Away, HomeDefender)
        };

        var result = Compute(game, sequence: 30);

        result.Facts.Should().NotContain(f => f.FactType == FactTypes.GameCount);
        result.Dropped.Should().ContainSingle(d => d.FactType == FactTypes.GameCount && d.AthleteSeasonId == HomeDefender);
        Fact(result, FactTypes.GameCountAgainst, AwayPasser).Value.Should().Be(2);
        TeamFact(result).Value.Should().Be(2);
    }

    [Fact]
    public void TeamTakeaways_CountInterceptionsAndLostFumbles_ForThisDefenseOnly()
    {
        var game = new List<GamePlay>
        {
            Play(10, "29", Away, 2, ("rusher", Guid.NewGuid()), ("recoverer", HomeDefender2), ("fumbler", Guid.NewGuid())),
            Play(15, "29", Home, 2, ("rusher", HomeRusher), ("recoverer", Guid.NewGuid()), ("fumbler", HomeRusher)), // other defense
            Play(20, "7", Away, -6, ("passer", AwayPasser), ("sackedBy", HomeDefender2)),                             // sack, not a takeaway
            Int(30, Away, HomeDefender)
        };

        var team = TeamFact(Compute(game, sequence: 30));

        team.FranchiseSeasonId.Should().Be(Home);
        team.Value.Should().Be(2);
    }

    [Fact]
    public void SplitSack_CountsForEachSacker_AndTheTeamOnce()
    {
        var game = new List<GamePlay>
        {
            Play(10, "7", Away, -5, ("passer", AwayPasser), ("sackedBy", HomeDefender)),
            Play(30, "7", Away, -7, ("passer", AwayPasser), ("sackedBy", HomeDefender), ("sackedBy", HomeDefender2))
        };

        var result = Compute(game, sequence: 30);

        Fact(result, FactTypes.GameCount, HomeDefender).Value.Should().Be(2);
        Fact(result, FactTypes.GameCount, HomeDefender2).Value.Should().Be(1);
        Fact(result, FactTypes.GameCountAgainst, AwayPasser).Value.Should().Be(2);
        TeamFact(result).Value.Should().Be(2);
    }

    [Theory]
    [InlineData(35, true)]   // longer than the 30-yarder
    [InlineData(30, false)]  // a tie isn't "the longest"
    [InlineData(25, false)]
    public void ExplosiveRun_IsTheGameLongestOnlyWhenStrictlyLonger(int yards, bool longest)
    {
        var game = new List<GamePlay>
        {
            Rush(10, Away, Guid.NewGuid(), 30),
            Rush(30, Home, HomeRusher, yards)
        };

        var result = Compute(game, sequence: 30);

        if (longest)
            Fact(result, FactTypes.GameLongest, HomeRusher).Value.Should().Be(yards);
        else
            result.Facts.Should().NotContain(f => f.FactType == FactTypes.GameLongest);
    }

    [Fact]
    public void LongerExplosiveRun_RecordsTheRunItBeat()
    {
        var result = Compute([Rush(10, Away, Guid.NewGuid(), 30), Rush(30, Home, HomeRusher, 35)], sequence: 30);

        Fact(result, FactTypes.GameLongest, HomeRusher).Provenance["previousLongest"].Should().Be(30);
    }

    [Fact]
    public void FirstExplosiveRun_IsNotTheGameLongest_WithNoEarlierRuns()
    {
        var result = Compute([Rush(30, Home, HomeRusher, 22)], sequence: 30);

        result.Facts.Should().NotContain(f => f.FactType == FactTypes.GameLongest);
    }

    [Fact]
    public void FirstExplosiveRun_IsNotTheGameLongest_WhenItOnlyBeatShortRuns()
    {
        var game = new List<GamePlay>
        {
            Rush(10, Away, Guid.NewGuid(), 0),
            Rush(20, Away, Guid.NewGuid(), PlayClassifier.ExplosiveRunYards - 1),
            Rush(30, Home, HomeRusher, 17)
        };

        var result = Compute(game, sequence: 30);

        result.Facts.Should().NotContain(f => f.FactType == FactTypes.GameLongest);
    }

    [Fact]
    public void UnknownDefense_DropsTheTeamFact()
    {
        var result = GameFacts.Compute(
            Current(Int(30, Away, HomeDefender)),
            PlayClassifier.Classify(Current(Int(30, Away, HomeDefender))),
            [Int(30, Away, HomeDefender)],
            new Dictionary<Guid, Guid>());

        result.Facts.Should().NotContain(f => f.FactType == FactTypes.TeamGameCount);
        result.Dropped.Should().Contain(d => d.FactType == FactTypes.TeamGameCount);
    }

    private static FactResult Compute(List<GamePlay> game, long sequence)
    {
        var current = Current(game.Single(p => p.Sequence == sequence));
        return GameFacts.Compute(current, PlayClassifier.Classify(current), game, DefenseByOffense);
    }

    private static PlayFact Fact(FactResult result, string factType, Guid athlete) =>
        result.Facts.Single(f => f.FactType == factType && f.AthleteSeasonId == athlete);

    private static PlayFact TeamFact(FactResult result) =>
        result.Facts.Single(f => f.FactType == FactTypes.TeamGameCount);

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
