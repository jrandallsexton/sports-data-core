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
