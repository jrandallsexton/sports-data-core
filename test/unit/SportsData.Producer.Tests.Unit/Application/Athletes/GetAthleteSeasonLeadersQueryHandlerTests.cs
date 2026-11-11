using FluentAssertions;

using FluentValidation;

using Moq;

using SportsData.Core.Common;
using SportsData.Producer.Application.Athletes.Queries.GetAthleteSeasonLeaders;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Athletes;

/// <summary>
/// The athlete season leaderboard. Seeds are handcrafted for the same reason
/// as the matchup-summaries tests: the joins span several tables and stray
/// auto-populated navigations would seed phantom rows.
/// </summary>
public class GetAthleteSeasonLeadersQueryHandlerTests : ProducerTestBase<GetAthleteSeasonLeadersQueryHandler>
{
    private const int SeasonYear = 2026;
    private static readonly Guid QbPositionId = Guid.NewGuid();
    private static readonly DateTime Older = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Newer = new(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);

    public GetAthleteSeasonLeadersQueryHandlerTests()
    {
        // Real validator: an auto-mocked IValidator returns a null result.
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(x => x.UtcNow())
            .Returns(new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc));
        Mocker.Use<IValidator<GetAthleteSeasonLeadersQuery>>(
            new GetAthleteSeasonLeadersQueryValidator(dateTimeProvider.Object));

        Mocker.GetMock<SportsData.Core.DependencyInjection.IAppMode>()
            .Setup(x => x.CurrentSport)
            .Returns(Sport.FootballNcaa);

        FootballDataContext.AthletePositions.Add(new AthletePosition
        {
            Id = QbPositionId,
            Name = "Quarterback",
            DisplayName = "Quarterback",
            Abbreviation = "QB",
        });
    }

    [Fact]
    public async Task Execute_RanksTotalsDescending_FbsOnly_TiesShareRank()
    {
        var fbs = SeedFranchiseSeason("oregon-state", "Oregon State");
        var fcs = SeedFranchiseSeason("montana", "Montana", "NCAAF|NCAA|fcs|big-sky");

        SeedStatDoc(SeedAthleteSeason(fbs, "Braden", "Atkinson").Id, Newer, games: 9, passYds: 3169);
        SeedStatDoc(SeedAthleteSeason(fbs, "Alpha", "Tied").Id, Newer, games: 9, passYds: 2000);
        SeedStatDoc(SeedAthleteSeason(fbs, "Beta", "Tied").Id, Newer, games: 9, passYds: 2000);
        SeedStatDoc(SeedAthleteSeason(fbs, "Gamma", "Fourth").Id, Newer, games: 9, passYds: 1500);
        SeedStatDoc(SeedAthleteSeason(fcs, "Fcs", "Passer").Id, Newer, games: 9, passYds: 4000);
        await FootballDataContext.SaveChangesAsync();

        var result = await Mocker.CreateInstance<GetAthleteSeasonLeadersQueryHandler>().ExecuteAsync(new GetAthleteSeasonLeadersQuery(SeasonYear, "passing", "passingYards"));

        result.IsSuccess.Should().BeTrue();
        var leaders = result.Value.Leaders;
        leaders.Select(l => l.AthleteName).Should().Equal(
            "Braden Atkinson", "Alpha Tied", "Beta Tied", "Gamma Fourth");
        leaders.Select(l => l.Rank).Should().Equal(1, 2, 2, 4);
        leaders[0].Value.Should().Be(3169);
        leaders[0].FranchiseSlug.Should().Be("oregon-state");
        leaders[0].Position.Should().Be("QB");
        result.Value.StatDisplayName.Should().Be("Passing Yards");
    }

    [Fact]
    public async Task Execute_UsesNewestStatDocPerAthleteSeason()
    {
        // Re-sourcing leaves duplicate docs; the stale one must neither rank
        // nor list the athlete twice.
        var fs = SeedFranchiseSeason("smu", "SMU");
        var athlete = SeedAthleteSeason(fs, "Kevin", "Jennings");
        SeedStatDoc(athlete.Id, Older, games: 8, passYds: 9999);
        SeedStatDoc(athlete.Id, Newer, games: 9, passYds: 2764);
        await FootballDataContext.SaveChangesAsync();

        var result = await Mocker.CreateInstance<GetAthleteSeasonLeadersQueryHandler>().ExecuteAsync(new GetAthleteSeasonLeadersQuery(SeasonYear, "passing", "passingYards"));

        result.Value.Leaders.Should().ContainSingle()
            .Which.Value.Should().Be(2764);
    }

    [Fact]
    public async Task Execute_PerGame_DividesByGamesPlayed_AndAppliesSeventyFivePercentFloor()
    {
        var fs = SeedFranchiseSeason("arizona", "Arizona");
        var opponent = SeedFranchiseSeason("utah", "Utah");
        SeedFinalizedGames(fs, opponent, count: 8);

        SeedStatDoc(SeedAthleteSeason(fs, "Noah", "Fifita").Id, Newer, games: 8, passYds: 2794);
        // 5 of 8 games is under the 75% floor (6), despite the higher average.
        SeedStatDoc(SeedAthleteSeason(fs, "Part", "Timer").Id, Newer, games: 5, passYds: 1800);
        await FootballDataContext.SaveChangesAsync();

        var result = await Mocker.CreateInstance<GetAthleteSeasonLeadersQueryHandler>().ExecuteAsync(
            new GetAthleteSeasonLeadersQuery(SeasonYear, "passing", "passingYards", PerGame: true));

        var leader = result.Value.Leaders.Should().ContainSingle().Subject;
        leader.AthleteName.Should().Be("Noah Fifita");
        leader.Value.Should().Be(349.3m);
        leader.DisplayValue.Should().Be("349.3");
        result.Value.MinGamesPlayedShare.Should().Be(0.75m);
    }

    [Fact]
    public async Task Execute_HonorsLimit()
    {
        var fs = SeedFranchiseSeason("north-texas", "North Texas");
        for (var i = 1; i <= 5; i++)
        {
            SeedStatDoc(SeedAthleteSeason(fs, "Passer", $"Number{i}").Id, Newer, games: 9, passYds: 100 * i);
        }
        await FootballDataContext.SaveChangesAsync();

        var result = await Mocker.CreateInstance<GetAthleteSeasonLeadersQueryHandler>().ExecuteAsync(
            new GetAthleteSeasonLeadersQuery(SeasonYear, "passing", "passingYards", Limit: 3));

        result.Value.Leaders.Select(l => l.Value).Should().Equal(500, 400, 300);
    }

    [Fact]
    public async Task Execute_WhenLimitSplitsATie_KeepsTheAlphabeticallyFirst()
    {
        var fs = SeedFranchiseSeason("tulane", "Tulane");
        SeedStatDoc(SeedAthleteSeason(fs, "Top", "Passer").Id, Newer, games: 9, passYds: 3000);
        // Ids are random, so ordering by id would keep an arbitrary one of these.
        foreach (var first in new[] { "Echo", "Delta", "Alpha", "Charlie", "Bravo" })
        {
            SeedStatDoc(SeedAthleteSeason(fs, first, "Tied").Id, Newer, games: 9, passYds: 2000);
        }
        await FootballDataContext.SaveChangesAsync();

        var result = await Mocker.CreateInstance<GetAthleteSeasonLeadersQueryHandler>().ExecuteAsync(
            new GetAthleteSeasonLeadersQuery(SeasonYear, "passing", "passingYards", Limit: 3));

        result.Value.Leaders.Select(l => l.AthleteName).Should().Equal(
            "Top Passer", "Alpha Tied", "Bravo Tied");
        result.Value.Leaders.Select(l => l.Rank).Should().Equal(1, 2, 2);
    }

    [Fact]
    public async Task Execute_WhenLimitOutOfRange_ReturnsValidationFailure()
    {
        var result = await Mocker.CreateInstance<GetAthleteSeasonLeadersQueryHandler>().ExecuteAsync(
            new GetAthleteSeasonLeadersQuery(SeasonYear, "passing", "passingYards", Limit: 0));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
    }

    private FranchiseSeason SeedFranchiseSeason(string slug, string name, string groupSeasonMap = "NCAAF|NCAA|fbs|pac-12")
    {
        var fs = new FranchiseSeason
        {
            Id = Guid.NewGuid(),
            FranchiseId = Guid.NewGuid(),
            SeasonYear = SeasonYear,
            Slug = slug,
            Name = name,
            Location = name,
            Abbreviation = slug[..3].ToUpperInvariant(),
            DisplayName = name,
            DisplayNameShort = name,
            ColorCodeHex = "000000",
            GroupSeasonMap = groupSeasonMap,
            IsActive = true,
        };
        FootballDataContext.FranchiseSeasons.Add(fs);
        return fs;
    }

    private AthleteSeason SeedAthleteSeason(FranchiseSeason fs, string firstName, string lastName)
    {
        var season = new FootballAthleteSeason
        {
            Id = Guid.NewGuid(),
            AthleteId = Guid.NewGuid(),
            FranchiseSeasonId = fs.Id,
            PositionId = QbPositionId,
            FirstName = firstName,
            LastName = lastName,
            DisplayName = $"{firstName} {lastName}",
            IsActive = true,
        };
        FootballDataContext.AthleteSeasons.Add(season);
        return season;
    }

    private void SeedStatDoc(Guid athleteSeasonId, DateTime createdUtc, decimal games, decimal passYds)
    {
        var doc = new AthleteSeasonStatistic
        {
            Id = Guid.NewGuid(),
            AthleteSeasonId = athleteSeasonId,
            SplitId = "0",
            SplitName = "Season",
            SplitAbbreviation = "Any",
            SplitType = "season",
            CreatedUtc = createdUtc,
        };
        var general = new AthleteSeasonStatisticCategory
        {
            Id = Guid.NewGuid(),
            AthleteSeasonStatisticId = doc.Id,
            Name = "general",
            DisplayName = "General",
            ShortDisplayName = "General",
            Abbreviation = "gen",
        };
        general.Stats.Add(new AthleteSeasonStatisticStat
        {
            Id = Guid.NewGuid(),
            AthleteSeasonStatisticCategoryId = general.Id,
            Name = "gamesPlayed",
            DisplayName = "Games Played",
            ShortDisplayName = "GP",
            Abbreviation = "GP",
            DisplayValue = games.ToString(),
            Value = games,
        });
        var passing = new AthleteSeasonStatisticCategory
        {
            Id = Guid.NewGuid(),
            AthleteSeasonStatisticId = doc.Id,
            Name = "passing",
            DisplayName = "Passing",
            ShortDisplayName = "Passing",
            Abbreviation = "pass",
        };
        passing.Stats.Add(new AthleteSeasonStatisticStat
        {
            Id = Guid.NewGuid(),
            AthleteSeasonStatisticCategoryId = passing.Id,
            Name = "passingYards",
            DisplayName = "Passing Yards",
            ShortDisplayName = "YDS",
            Abbreviation = "YDS",
            DisplayValue = passYds.ToString("#,0"),
            Value = passYds,
        });
        FootballDataContext.AthleteSeasonStatistics.Add(doc);
        FootballDataContext.AthleteSeasonStatisticCategories.AddRange(general, passing);
    }

    private void SeedFinalizedGames(FranchiseSeason team, FranchiseSeason opponent, int count)
    {
        var phase = new SeasonPhase
        {
            Id = Guid.NewGuid(),
            SeasonId = Guid.NewGuid(),
            TypeCode = 2,
            Name = "Regular Season",
            Abbreviation = "reg",
            Slug = "regular-season",
            Year = SeasonYear,
        };
        FootballDataContext.SeasonPhases.Add(phase);

        for (var i = 0; i < count; i++)
        {
            FootballDataContext.Contests.Add(new FootballContest
            {
                Id = Guid.NewGuid(),
                Name = $"{opponent.Name} at {team.Name}",
                ShortName = "final",
                Sport = Sport.FootballNcaa,
                SeasonYear = SeasonYear,
                StartDateUtc = new DateTime(SeasonYear, 9, 6, 0, 0, 0, DateTimeKind.Utc).AddDays(7 * i),
                HomeTeamFranchiseSeasonId = team.Id,
                AwayTeamFranchiseSeasonId = opponent.Id,
                SeasonPhaseId = phase.Id,
                HomeScore = 28,
                AwayScore = 21,
                FinalizedUtc = new DateTime(SeasonYear, 9, 7, 0, 0, 0, DateTimeKind.Utc).AddDays(7 * i),
            });
        }
    }
}
