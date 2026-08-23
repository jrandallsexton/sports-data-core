#nullable enable

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsByContestIds;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;
using SportsData.Producer.Infrastructure.Sql;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests.Queries.GetMatchupsByContestIds;

/// <summary>
/// Tests for the probables-stitch helper on GetMatchupsByContestIdsQueryHandler.
/// The main ExecuteAsync runs raw SQL via Dapper and isn't testable through the
/// EF InMemory provider — these tests target only the EF-side companion fetch
/// that augments the SQL result with MLB probable starting pitcher info.
/// </summary>
public class GetMatchupsByContestIdsQueryHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetProbablePitchers_HappyPath_ReturnsHomeAndAwayPitchers()
    {
        // arrange — one MLB matchup with both home and away probable SPs.
        var ctx = NewBaseballContext();
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        SeedContestWithCompetition(ctx, contestId, competitionId);

        var (homeCompetitorId, _) = SeedCompetitorWithProbable(ctx, competitionId,
            homeAway: "home", athleteName: "Home Ace", headshot: "https://cdn/home.png");
        var (awayCompetitorId, _) = SeedCompetitorWithProbable(ctx, competitionId,
            homeAway: "away", athleteName: "Away Ace", headshot: "https://cdn/away.png");

        await ctx.SaveChangesAsync();

        var sut = NewSut(ctx);

        // act
        var result = await sut.GetProbablePitchersAsync(new[] { contestId }, CancellationToken.None);

        // assert
        result.Should().ContainKey(contestId);
        var pair = result[contestId];
        pair.Home.Should().NotBeNull();
        pair.Home!.DisplayName.Should().Be("Home Ace");
        pair.Home.HeadshotUrl.Should().Be("https://cdn/home.png");
        pair.Away.Should().NotBeNull();
        pair.Away!.DisplayName.Should().Be("Away Ace");
        pair.Away.HeadshotUrl.Should().Be("https://cdn/away.png");

        // unused locals quieted
        _ = homeCompetitorId;
        _ = awayCompetitorId;
    }

    [Fact]
    public async Task GetProbablePitchers_FiltersOutNonStartingPitcherRoles()
    {
        // arrange — seed a probable with a non-SP role; should be ignored.
        var ctx = NewBaseballContext();
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        SeedContestWithCompetition(ctx, contestId, competitionId);

        SeedCompetitorWithProbable(ctx, competitionId,
            homeAway: "home", athleteName: "Closer Carl", headshot: null,
            probableName: "probableCloser");

        await ctx.SaveChangesAsync();

        var sut = NewSut(ctx);

        // act
        var result = await sut.GetProbablePitchersAsync(new[] { contestId }, CancellationToken.None);

        // assert — no entry for this contest because no SP probable exists.
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProbablePitchers_NonBaseballContext_ReturnsEmpty()
    {
        // arrange — use a Football context. The helper should sport-gate
        // and short-circuit without a query.
        var footballCtx = new FootballDataContext(
            new DbContextOptionsBuilder<FootballDataContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()[..8])
                .Options);

        var handler = new GetMatchupsByContestIdsQueryHandler(
            NullLogger<GetMatchupsByContestIdsQueryHandler>.Instance,
            footballCtx,
            new ProducerSqlQueryProvider());

        // act
        var result = await handler.GetProbablePitchersAsync(new[] { Guid.NewGuid() }, CancellationToken.None);

        // assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProbablePitchers_MultipleCompetitionsPerContest_PicksDeterministicallyByCompetitorId()
    {
        // arrange — single Contest with TWO Competitions (e.g. a stale
        // reschedule artifact), each with its own home CompetitionCompetitor
        // and SP probable. The unique index allows this — it's per
        // CompetitionCompetitor, not per Contest. Without a stable OrderBy,
        // the dict-stitch was overwriting on the last row Postgres
        // happened to return.
        var ctx = NewBaseballContext();
        var contestId = Guid.NewGuid();
        var competitionA = Guid.NewGuid();
        var competitionB = Guid.NewGuid();

        // Two Competitions hanging off the same Contest.
        ctx.Contests.Add(new BaseballContest
        {
            Id = contestId,
            Name = "Test",
            ShortName = "TST",
            SeasonYear = 2026,
            Sport = SportsData.Core.Common.Sport.BaseballMlb,
            StartDateUtc = FixedNow,
            HomeTeamFranchiseSeasonId = Guid.NewGuid(),
            AwayTeamFranchiseSeasonId = Guid.NewGuid(),
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        ctx.Competitions.Add(new BaseballCompetition
        {
            Id = competitionA,
            ContestId = contestId,
            Date = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        ctx.Competitions.Add(new BaseballCompetition
        {
            Id = competitionB,
            ContestId = contestId,
            Date = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        // Pin CompetitorIds so we can predict the deterministic winner
        // (lowest CompetitorId by GUID compare).
        var lowerCompetitorId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higherCompetitorId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        SeedHomeCompetitorWithProbable(ctx, competitionA, lowerCompetitorId, "Lower Wins");
        SeedHomeCompetitorWithProbable(ctx, competitionB, higherCompetitorId, "Higher Loses");

        await ctx.SaveChangesAsync();

        var sut = NewSut(ctx);

        // act — call twice, assert same answer both times.
        var first = await sut.GetProbablePitchersAsync(new[] { contestId }, CancellationToken.None);
        var second = await sut.GetProbablePitchersAsync(new[] { contestId }, CancellationToken.None);

        // assert
        first[contestId].Home!.DisplayName.Should().Be("Lower Wins");
        second[contestId].Home!.DisplayName.Should().Be(first[contestId].Home!.DisplayName);
    }

    [Fact]
    public async Task GetProbablePitchers_PicksEarliestHeadshotByCreatedUtc()
    {
        // arrange — seed an athlete with two images; expect the earliest one.
        var ctx = NewBaseballContext();
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        SeedContestWithCompetition(ctx, contestId, competitionId);

        var competitorId = Guid.NewGuid();
        ctx.CompetitionCompetitors.Add(new BaseballCompetitionCompetitor
        {
            Id = competitorId,
            CompetitionId = competitionId,
            HomeAway = "home",
            FranchiseSeasonId = Guid.NewGuid(),
            Order = 1,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        var athleteId = Guid.NewGuid();
        var athleteSeasonId = Guid.NewGuid();
        ctx.Set<BaseballAthlete>().Add(new BaseballAthlete
        {
            Id = athleteId,
            FirstName = "Ace",
            LastName = "Pitcher",
            DisplayName = "Ace Pitcher",
            ShortName = "A. Pitcher",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        ctx.AthleteSeasons.Add(new BaseballAthleteSeason
        {
            Id = athleteSeasonId,
            AthleteId = athleteId,
            DisplayName = "Ace Pitcher",
            PositionId = Guid.NewGuid(),
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        ctx.Set<AthleteImage>().Add(new AthleteImage
        {
            Id = Guid.NewGuid(),
            AthleteId = athleteId,
            OriginalUrlHash = "hashLater",
            Uri = new Uri("https://cdn/later.png"),
            CreatedUtc = FixedNow.AddDays(2),
            CreatedBy = Guid.NewGuid()
        });
        ctx.Set<AthleteImage>().Add(new AthleteImage
        {
            Id = Guid.NewGuid(),
            AthleteId = athleteId,
            OriginalUrlHash = "hashEarlier",
            Uri = new Uri("https://cdn/earlier.png"),
            CreatedUtc = FixedNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        });
        ctx.CompetitionCompetitorProbables.Add(new CompetitionCompetitorProbable
        {
            Id = Guid.NewGuid(),
            CompetitionCompetitorId = competitorId,
            AthleteSeasonId = athleteSeasonId,
            EspnPlayerId = 1,
            Name = "probableStartingPitcher",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await ctx.SaveChangesAsync();

        var sut = NewSut(ctx);

        // act
        var result = await sut.GetProbablePitchersAsync(new[] { contestId }, CancellationToken.None);

        // assert — earliest CreatedUtc image wins.
        result.Should().ContainKey(contestId);
        result[contestId].Home!.HeadshotUrl.Should().Be("https://cdn/earlier.png");
    }

    // ─── Live snap state (cold-start) ────────────────────────────────────
    // Sourced from the LATEST PLAY, not CompetitionSituation: that row is
    // created once per competition and never updated, so it is frozen at
    // the game's first snap.

    [Fact]
    public async Task GetLiveSituations_UsesLatestPlay_NotTheFirst()
    {
        // arrange — three plays; only the highest SequenceNumber describes
        // the current snap. A non-deterministic pick would surface the
        // opening kickoff for the whole game.
        var ctx = NewFootballContext();
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        SeedFootballContestWithCompetition(ctx, contestId, competitionId);

        // SequenceNumber is TEXT: a lexicographic sort would rank "97300"
        // below "500" only if widths matched, and would rank "9" above
        // "100000" outright — so the widths here deliberately differ.
        SeedFootballPlay(ctx, competitionId, sequence: "100", down: 1, distance: 10, yardLine: 35);
        SeedFootballPlay(ctx, competitionId, sequence: "97300", down: 2, distance: 5, yardLine: 45);
        SeedFootballPlay(ctx, competitionId, sequence: "500", down: 3, distance: 8, yardLine: 60);
        await ctx.SaveChangesAsync();

        // act
        var result = await NewFootballSut(ctx)
            .GetLiveSituationsAsync(new[] { contestId }, CancellationToken.None);

        // assert
        result.Should().ContainKey(contestId);
        result[contestId].Down.Should().Be(2);
        result[contestId].Distance.Should().Be(5);
        result[contestId].BallOnYardLine.Should().Be(45);
    }

    [Fact]
    public async Task GetLiveSituations_DownZero_SurfacesAsNullButKeepsTheSpot()
    {
        // arrange — ESPN reports down 0 at kickoffs, extra points and end of
        // period. Publishing it verbatim would render "0th & 0".
        var ctx = NewFootballContext();
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        SeedFootballContestWithCompetition(ctx, contestId, competitionId);
        SeedFootballPlay(ctx, competitionId, sequence: "1", down: 0, distance: 0, yardLine: 35);
        await ctx.SaveChangesAsync();

        // act
        var result = await NewFootballSut(ctx)
            .GetLiveSituationsAsync(new[] { contestId }, CancellationToken.None);

        // assert
        result[contestId].Down.Should().BeNull();
        result[contestId].Distance.Should().BeNull();
        result[contestId].BallOnYardLine.Should().Be(35);
    }

    [Fact]
    public async Task GetLiveSituations_MissingEndState_FallsBackToTheStartPair()
    {
        // arrange — down and distance travel together; an end distance
        // paired with a start down would describe a snap that never existed.
        var ctx = NewFootballContext();
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        SeedFootballContestWithCompetition(ctx, contestId, competitionId);
        SeedFootballPlay(
            ctx, competitionId, sequence: "10",
            down: null, distance: null, yardLine: null,
            startDown: 3, startDistance: 4, startYardLine: 22);
        await ctx.SaveChangesAsync();

        // act
        var result = await NewFootballSut(ctx)
            .GetLiveSituationsAsync(new[] { contestId }, CancellationToken.None);

        // assert
        result[contestId].Down.Should().Be(3);
        result[contestId].Distance.Should().Be(4);
        result[contestId].BallOnYardLine.Should().Be(22);
    }

    [Fact]
    public async Task GetLiveSituations_PartialEndPair_PrefersTheCompleteStartPair()
    {
        // arrange — an end DOWN with no end DISTANCE is half a snap state.
        // Taking it would render "2nd" with no distance while a complete
        // "3rd & 4" was available.
        var ctx = NewFootballContext();
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        SeedFootballContestWithCompetition(ctx, contestId, competitionId);
        SeedFootballPlay(
            ctx, competitionId, sequence: "10",
            down: 2, distance: null, yardLine: 40,
            startDown: 3, startDistance: 4, startYardLine: 22);
        await ctx.SaveChangesAsync();

        // act
        var result = await NewFootballSut(ctx)
            .GetLiveSituationsAsync(new[] { contestId }, CancellationToken.None);

        // assert — the complete pair wins; the end yard line still stands.
        result[contestId].Down.Should().Be(3);
        result[contestId].Distance.Should().Be(4);
        result[contestId].BallOnYardLine.Should().Be(40);
    }

    [Fact]
    public async Task GetLiveSituations_MalformedSequence_NeverOutranksANumericOne()
    {
        // arrange — the SQL lateral orders only entirely-numeric ordinals
        // and sorts the rest last. The C# stitch must apply the identical
        // rule or the two paths could select different plays.
        var ctx = NewFootballContext();
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        SeedFootballContestWithCompetition(ctx, contestId, competitionId);
        SeedFootballPlay(ctx, competitionId, sequence: "900", down: 2, distance: 5, yardLine: 45);
        SeedFootballPlay(ctx, competitionId, sequence: "99999x", down: 4, distance: 1, yardLine: 12);
        await ctx.SaveChangesAsync();

        // act
        var result = await NewFootballSut(ctx)
            .GetLiveSituationsAsync(new[] { contestId }, CancellationToken.None);

        // assert — the numeric ordinal wins despite the malformed one being
        // lexicographically larger.
        result[contestId].Down.Should().Be(2);
        result[contestId].BallOnYardLine.Should().Be(45);
    }

    [Fact]
    public async Task GetLiveSituations_OversizedNumericSequence_SortsLastNotFirst()
    {
        // arrange — an all-digits ordinal too large for a 64-bit integer.
        // The SQL lateral bounds the cast at 18 digits because ::bigint on
        // anything longer raises "value out of range" and would fail the
        // whole query; this side must apply the identical bound or the two
        // paths would select different plays.
        var ctx = NewFootballContext();
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        SeedFootballContestWithCompetition(ctx, contestId, competitionId);
        SeedFootballPlay(ctx, competitionId, sequence: "900", down: 2, distance: 5, yardLine: 45);
        SeedFootballPlay(
            ctx, competitionId, sequence: "99999999999999999999999",
            down: 4, distance: 1, yardLine: 12);
        await ctx.SaveChangesAsync();

        // act
        var result = await NewFootballSut(ctx)
            .GetLiveSituationsAsync(new[] { contestId }, CancellationToken.None);

        // assert — the in-range ordinal wins despite being numerically and
        // lexicographically smaller.
        result[contestId].Down.Should().Be(2);
        result[contestId].BallOnYardLine.Should().Be(45);
    }

    [Fact]
    public async Task GetLiveSituations_LeadingZeroSequence_OrdersByItsNumericValue()
    {
        // Leading zeroes cast cleanly on both sides ("000123" -> 123), so
        // the padded ordinal must lose to the larger unpadded one.
        var ctx = NewFootballContext();
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        SeedFootballContestWithCompetition(ctx, contestId, competitionId);
        SeedFootballPlay(ctx, competitionId, sequence: "000123", down: 1, distance: 10, yardLine: 20);
        SeedFootballPlay(ctx, competitionId, sequence: "900", down: 3, distance: 2, yardLine: 61);
        await ctx.SaveChangesAsync();

        var result = await NewFootballSut(ctx)
            .GetLiveSituationsAsync(new[] { contestId }, CancellationToken.None);

        result[contestId].Down.Should().Be(3);
        result[contestId].BallOnYardLine.Should().Be(61);
    }

    [Fact]
    public async Task GetLiveSituations_MultipleCompetitionsPerContest_TakesTheHighestSequenceAcrossThem()
    {
        // arrange — one Contest hosting TWO Competitions (a stale reschedule
        // artifact; the probables stitch documents the same shape). The
        // latest play must be resolved across the whole contest, matching
        // the SQL lateral's contest correlation — a per-competition pick
        // would depend on which competition happened to be visited first.
        var ctx = NewFootballContext();
        var contestId = Guid.NewGuid();
        var firstCompetition = Guid.NewGuid();
        var secondCompetition = Guid.NewGuid();
        SeedFootballContestWithCompetition(ctx, contestId, firstCompetition);
        ctx.Competitions.Add(new FootballCompetition
        {
            Id = secondCompetition,
            ContestId = contestId,
            Date = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        SeedFootballPlay(ctx, firstCompetition, sequence: "4000", down: 1, distance: 10, yardLine: 30);
        SeedFootballPlay(ctx, secondCompetition, sequence: "9500", down: 3, distance: 2, yardLine: 71);
        await ctx.SaveChangesAsync();

        // act
        var result = await NewFootballSut(ctx)
            .GetLiveSituationsAsync(new[] { contestId }, CancellationToken.None);

        // assert — one entry for the contest, carrying the higher ordinal.
        result.Should().ContainSingle();
        result[contestId].Down.Should().Be(3);
        result[contestId].Distance.Should().Be(2);
        result[contestId].BallOnYardLine.Should().Be(71);
    }

    [Fact]
    public async Task GetLiveSituations_PossessionComesFromTheEndOfPlayTeam()
    {
        // arrange — a punt: the start team kicked it away, the end team
        // lines up next. Reading Start would credit the punting team with
        // the ball, which is wrong on 1 in 6 plays.
        var ctx = NewFootballContext();
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var punting = Guid.NewGuid();
        var receiving = Guid.NewGuid();
        SeedFootballContestWithCompetition(ctx, contestId, competitionId);
        SeedFootballPlay(
            ctx, competitionId, sequence: "42",
            down: 1, distance: 10, yardLine: 25,
            startFranchiseSeasonId: punting,
            endFranchiseSeasonId: receiving);
        await ctx.SaveChangesAsync();

        // act
        var result = await NewFootballSut(ctx)
            .GetLiveSituationsAsync(new[] { contestId }, CancellationToken.None);

        // assert
        result[contestId].PossessionFranchiseSeasonId.Should().Be(receiving);
    }

    [Fact]
    public async Task GetLiveSituations_MissingEndTeam_FallsBackToTheStartTeam()
    {
        var ctx = NewFootballContext();
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var offense = Guid.NewGuid();
        SeedFootballContestWithCompetition(ctx, contestId, competitionId);
        SeedFootballPlay(
            ctx, competitionId, sequence: "7",
            down: 2, distance: 6, yardLine: 55,
            startFranchiseSeasonId: offense,
            endFranchiseSeasonId: null);
        await ctx.SaveChangesAsync();

        var result = await NewFootballSut(ctx)
            .GetLiveSituationsAsync(new[] { contestId }, CancellationToken.None);

        result[contestId].PossessionFranchiseSeasonId.Should().Be(offense);
    }

    [Fact]
    public async Task GetLiveSituations_NonFootballContext_ReturnsEmpty()
    {
        // Baseball plays carry no per-play count/runner state, so MLB gets
        // the sport-neutral SQL fields and nothing from this stitch.
        var result = await NewSut(NewBaseballContext())
            .GetLiveSituationsAsync(new[] { Guid.NewGuid() }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLiveSituations_NoPlaysYet_ReturnsEmpty()
    {
        var ctx = NewFootballContext();
        var contestId = Guid.NewGuid();
        SeedFootballContestWithCompetition(ctx, contestId, Guid.NewGuid());
        await ctx.SaveChangesAsync();

        var result = await NewFootballSut(ctx)
            .GetLiveSituationsAsync(new[] { contestId }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static FootballDataContext NewFootballContext() =>
        new(new DbContextOptionsBuilder<FootballDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()[..8])
            .Options);

    private static GetMatchupsByContestIdsQueryHandler NewFootballSut(FootballDataContext ctx) =>
        new(NullLogger<GetMatchupsByContestIdsQueryHandler>.Instance,
            ctx,
            new ProducerSqlQueryProvider());

    private static void SeedFootballContestWithCompetition(
        FootballDataContext ctx,
        Guid contestId,
        Guid competitionId)
    {
        ctx.Contests.Add(new FootballContest
        {
            Id = contestId,
            Name = "Test",
            ShortName = "TST",
            SeasonYear = 2026,
            Sport = SportsData.Core.Common.Sport.FootballNfl,
            StartDateUtc = FixedNow,
            HomeTeamFranchiseSeasonId = Guid.NewGuid(),
            AwayTeamFranchiseSeasonId = Guid.NewGuid(),
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        ctx.Competitions.Add(new FootballCompetition
        {
            Id = competitionId,
            ContestId = contestId,
            Date = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
    }

    private static void SeedFootballPlay(
        FootballDataContext ctx,
        Guid competitionId,
        string sequence,
        int? down,
        int? distance,
        int? yardLine,
        int? startDown = null,
        int? startDistance = null,
        int? startYardLine = null,
        Guid? startFranchiseSeasonId = null,
        Guid? endFranchiseSeasonId = null)
    {
        ctx.CompetitionPlays.Add(new FootballCompetitionPlay
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            EspnId = Guid.NewGuid().ToString("N"),
            TypeId = "5",
            SequenceNumber = sequence,
            Text = $"Play {sequence}",
            EndDown = down,
            EndDistance = distance,
            EndYardLine = yardLine,
            StartDown = startDown,
            StartDistance = startDistance,
            StartYardLine = startYardLine,
            StartFranchiseSeasonId = startFranchiseSeasonId,
            EndFranchiseSeasonId = endFranchiseSeasonId,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
    }

    private static BaseballDataContext NewBaseballContext() =>
        new(new DbContextOptionsBuilder<BaseballDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()[..8])
            .Options);

    private static GetMatchupsByContestIdsQueryHandler NewSut(BaseballDataContext ctx) =>
        new(NullLogger<GetMatchupsByContestIdsQueryHandler>.Instance,
            ctx,
            new ProducerSqlQueryProvider());

    private static void SeedContestWithCompetition(BaseballDataContext ctx, Guid contestId, Guid competitionId)
    {
        ctx.Contests.Add(new BaseballContest
        {
            Id = contestId,
            Name = "Test",
            ShortName = "TST",
            SeasonYear = 2026,
            Sport = SportsData.Core.Common.Sport.BaseballMlb,
            StartDateUtc = FixedNow,
            HomeTeamFranchiseSeasonId = Guid.NewGuid(),
            AwayTeamFranchiseSeasonId = Guid.NewGuid(),
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        ctx.Competitions.Add(new BaseballCompetition
        {
            Id = competitionId,
            ContestId = contestId,
            Date = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
    }

    // Variant that lets the test pin CompetitorId so the OrderBy in the
    // production code is exercised against a known relative order.
    private static void SeedHomeCompetitorWithProbable(
        BaseballDataContext ctx,
        Guid competitionId,
        Guid competitorId,
        string athleteName)
    {
        ctx.CompetitionCompetitors.Add(new BaseballCompetitionCompetitor
        {
            Id = competitorId,
            CompetitionId = competitionId,
            HomeAway = "home",
            FranchiseSeasonId = Guid.NewGuid(),
            Order = 0,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        var athleteId = Guid.NewGuid();
        var athleteSeasonId = Guid.NewGuid();
        ctx.Set<BaseballAthlete>().Add(new BaseballAthlete
        {
            Id = athleteId,
            FirstName = athleteName,
            LastName = "Doe",
            DisplayName = athleteName,
            ShortName = athleteName,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        ctx.AthleteSeasons.Add(new BaseballAthleteSeason
        {
            Id = athleteSeasonId,
            AthleteId = athleteId,
            DisplayName = athleteName,
            PositionId = Guid.NewGuid(),
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        ctx.CompetitionCompetitorProbables.Add(new CompetitionCompetitorProbable
        {
            Id = Guid.NewGuid(),
            CompetitionCompetitorId = competitorId,
            AthleteSeasonId = athleteSeasonId,
            EspnPlayerId = athleteName.GetHashCode(),
            Name = "probableStartingPitcher",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
    }

    private static (Guid CompetitorId, Guid AthleteSeasonId) SeedCompetitorWithProbable(
        BaseballDataContext ctx,
        Guid competitionId,
        string homeAway,
        string athleteName,
        string? headshot,
        string probableName = "probableStartingPitcher")
    {
        var competitorId = Guid.NewGuid();
        ctx.CompetitionCompetitors.Add(new BaseballCompetitionCompetitor
        {
            Id = competitorId,
            CompetitionId = competitionId,
            HomeAway = homeAway,
            FranchiseSeasonId = Guid.NewGuid(),
            Order = string.Equals(homeAway, "home", StringComparison.OrdinalIgnoreCase) ? 0 : 1,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        var athleteId = Guid.NewGuid();
        var athleteSeasonId = Guid.NewGuid();
        ctx.Set<BaseballAthlete>().Add(new BaseballAthlete
        {
            Id = athleteId,
            FirstName = athleteName.Split(' ')[0],
            LastName = athleteName.Split(' ').Length > 1 ? athleteName.Split(' ')[1] : "Doe",
            DisplayName = athleteName,
            ShortName = athleteName,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        ctx.AthleteSeasons.Add(new BaseballAthleteSeason
        {
            Id = athleteSeasonId,
            AthleteId = athleteId,
            DisplayName = athleteName,
            PositionId = Guid.NewGuid(),
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        if (headshot is not null)
        {
            ctx.Set<AthleteImage>().Add(new AthleteImage
            {
                Id = Guid.NewGuid(),
                AthleteId = athleteId,
                OriginalUrlHash = Guid.NewGuid().ToString("N").Substring(0, 16),
                Uri = new Uri(headshot),
                CreatedUtc = FixedNow,
                CreatedBy = Guid.NewGuid()
            });
        }
        ctx.CompetitionCompetitorProbables.Add(new CompetitionCompetitorProbable
        {
            Id = Guid.NewGuid(),
            CompetitionCompetitorId = competitorId,
            AthleteSeasonId = athleteSeasonId,
            EspnPlayerId = athleteName.GetHashCode(),
            Name = probableName,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        return (competitorId, athleteSeasonId);
    }
}
