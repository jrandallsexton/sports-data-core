using FluentAssertions;

using FluentValidation;

using Moq;

using SportsData.Core.Common;
using SportsData.Producer.Application.Athletes.Queries.GetAthleteMatchupSummaries;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Athletes;

/// <summary>
/// The athlete matchup-summaries feed. Seeds are handcrafted (no
/// AutoFixture graphs) because the handler's joins span seven tables and
/// stray auto-populated navigation properties would seed phantom rows.
/// </summary>
public class GetAthleteMatchupSummariesQueryHandlerTests : ProducerTestBase<GetAthleteMatchupSummariesQueryHandler>
{
    private static readonly Guid QbPositionId = Guid.NewGuid();
    private static readonly Guid KickerPositionId = Guid.NewGuid();
    private static readonly Guid ActiveStatusId = Guid.NewGuid();

    public GetAthleteMatchupSummariesQueryHandlerTests()
    {
        // The handler takes the REAL validator — an auto-mocked IValidator
        // returns a null ValidationResult and NREs before the code under
        // test runs.
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(x => x.UtcNow())
            .Returns(new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc));
        Mocker.Use<IValidator<GetAthleteMatchupSummariesQuery>>(
            new GetAthleteMatchupSummariesQueryValidator(dateTimeProvider.Object));

        // Default the app mode to NCAAFB; the NFL test overrides it. An
        // unset mock would report Sport 0 and silently skip the FBS filter
        // the NCAAFB tests exercise.
        SetAppMode(Sport.FootballNcaa);
    }

    private void SetAppMode(Sport sport)
    {
        Mocker.GetMock<SportsData.Core.DependencyInjection.IAppMode>()
            .Setup(x => x.CurrentSport)
            .Returns(sport);
    }

    private void SeedPositionAndStatus()
    {
        FootballDataContext.AthletePositions.Add(new AthletePosition
        {
            Id = QbPositionId,
            Name = "Quarterback",
            DisplayName = "Quarterback",
            Abbreviation = "QB",
        });
        // ESPN's kicker abbreviation is PK; the handler translates the UI's K.
        FootballDataContext.AthletePositions.Add(new AthletePosition
        {
            Id = KickerPositionId,
            Name = "Place Kicker",
            DisplayName = "Place Kicker",
            Abbreviation = "PK",
        });
        FootballDataContext.AthleteStatuses.Add(new AthleteStatus
        {
            Id = ActiveStatusId,
            ExternalId = "1",
            Name = "Active",
        });
    }

    private FranchiseSeason SeedFranchiseSeason(
        int seasonYear, string slug, string name, Guid? franchiseId = null, string groupSeasonMap = "NCAAF|NCAA|fbs|sec")
    {
        var fs = new FranchiseSeason
        {
            Id = Guid.NewGuid(),
            FranchiseId = franchiseId ?? Guid.NewGuid(),
            SeasonYear = seasonYear,
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

    private AthleteSeason SeedAthleteSeason(
        Guid athleteId, FranchiseSeason fs, string firstName, string lastName, Guid? positionId = null)
    {
        // TPH: the football DB stores FootballAthleteSeason rows.
        var season = new FootballAthleteSeason
        {
            Id = Guid.NewGuid(),
            AthleteId = athleteId,
            FranchiseSeasonId = fs.Id,
            PositionId = positionId ?? QbPositionId,
            StatusId = ActiveStatusId,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
        };
        FootballDataContext.AthleteSeasons.Add(season);
        return season;
    }

    private void SeedStatDoc(
        Guid athleteSeasonId,
        DateTime createdUtc,
        decimal gamesPlayed,
        decimal passYds)
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
            DisplayValue = gamesPlayed.ToString(),
            Value = gamesPlayed,
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
            DisplayValue = passYds.ToString(),
            Value = passYds,
        });
        FootballDataContext.AthleteSeasonStatistics.Add(doc);
        FootballDataContext.AthleteSeasonStatisticCategories.AddRange(general, passing);
    }

    private void SeedWeekContest(
        FranchiseSeason home, FranchiseSeason away, int seasonYear, int week, int phaseTypeCode = 2)
    {
        // Week resolves via SeasonWeek.Number scoped to the regular-season
        // phase — the handler deliberately ignores Contest.Week, which the
        // schedule import leaves null, and week numbers restart per phase.
        var phase = new SeasonPhase
        {
            Id = Guid.NewGuid(),
            SeasonId = Guid.NewGuid(),
            TypeCode = phaseTypeCode,
            Name = phaseTypeCode == 2 ? "Regular Season" : "Postseason",
            Abbreviation = phaseTypeCode == 2 ? "reg" : "post",
            Slug = phaseTypeCode == 2 ? "regular-season" : "post-season",
            Year = seasonYear,
        };
        FootballDataContext.SeasonPhases.Add(phase);
        var seasonWeek = new SeasonWeek
        {
            Id = Guid.NewGuid(),
            SeasonId = phase.SeasonId,
            SeasonPhaseId = phase.Id,
            Number = week,
        };
        FootballDataContext.SeasonWeeks.Add(seasonWeek);
        FootballDataContext.Contests.Add(new FootballContest
        {
            Id = Guid.NewGuid(),
            Name = $"{away.Name} at {home.Name}",
            ShortName = "game",
            Sport = Sport.FootballNcaa,
            SeasonYear = seasonYear,
            Week = null,
            SeasonWeekId = seasonWeek.Id,
            StartDateUtc = new DateTime(seasonYear, 9, 6, 0, 0, 0, DateTimeKind.Utc),
            HomeTeamFranchiseSeasonId = home.Id,
            AwayTeamFranchiseSeasonId = away.Id,
            SeasonPhaseId = phase.Id,
        });
    }

    /// <summary>Finalized contest with scores — feeds the K points-allowed metric.</summary>
    private void SeedFinalizedContest(
        FranchiseSeason home, FranchiseSeason away, int seasonYear, int homeScore, int awayScore)
    {
        FootballDataContext.Contests.Add(new FootballContest
        {
            Id = Guid.NewGuid(),
            Name = $"{away.Name} at {home.Name}",
            ShortName = "final",
            Sport = Sport.FootballNcaa,
            SeasonYear = seasonYear,
            StartDateUtc = new DateTime(seasonYear, 9, 13, 0, 0, 0, DateTimeKind.Utc),
            HomeTeamFranchiseSeasonId = home.Id,
            AwayTeamFranchiseSeasonId = away.Id,
            SeasonPhaseId = Guid.NewGuid(),
            HomeScore = homeScore,
            AwayScore = awayScore,
            FinalizedUtc = new DateTime(seasonYear, 9, 14, 0, 0, 0, DateTimeKind.Utc),
        });
    }

    /// <summary>Kicking statistic doc (FG made/att + XP) for the K column set.</summary>
    private void SeedKickingStatDoc(Guid athleteSeasonId, DateTime createdUtc, decimal gamesPlayed)
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
            DisplayValue = gamesPlayed.ToString(),
            Value = gamesPlayed,
        });
        var kicking = new AthleteSeasonStatisticCategory
        {
            Id = Guid.NewGuid(),
            AthleteSeasonStatisticId = doc.Id,
            Name = "kicking",
            DisplayName = "Kicking",
            ShortDisplayName = "Kicking",
            Abbreviation = "kick",
        };
        (string name, decimal value)[] stats =
        [
            ("fieldGoalsMade", 9), ("fieldGoalAttempts", 10), ("fieldGoalPct", 90.0m),
            ("longFieldGoalMade", 56), ("extraPointsMade", 14), ("extraPointAttempts", 14),
        ];
        foreach (var (name, value) in stats)
        {
            kicking.Stats.Add(new AthleteSeasonStatisticStat
            {
                Id = Guid.NewGuid(),
                AthleteSeasonStatisticCategoryId = kicking.Id,
                Name = name,
                DisplayName = name,
                ShortDisplayName = name,
                Abbreviation = name,
                DisplayValue = value.ToString(),
                Value = value,
            });
        }
        FootballDataContext.AthleteSeasonStatistics.Add(doc);
        FootballDataContext.AthleteSeasonStatisticCategories.AddRange(general, kicking);
    }

    /// <summary>One played game: <paramref name="gainer"/> gained
    /// <paramref name="passYds"/> against <paramref name="defense"/>.</summary>
    private void SeedPlayedGame(FranchiseSeason defense, FranchiseSeason gainer, decimal passYds)
    {
        var competitionId = Guid.NewGuid();
        FootballDataContext.CompetitionCompetitors.Add(new FootballCompetitionCompetitor
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            FranchiseSeasonId = defense.Id,
            HomeAway = "home",
        });
        var stat = new CompetitionCompetitorStatistic
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            FranchiseSeasonId = gainer.Id,
        };
        var passing = new CompetitionCompetitorStatisticCategory
        {
            Id = Guid.NewGuid(),
            CompetitionCompetitorStatisticId = stat.Id,
            Name = "passing",
        };
        passing.Stats.Add(new CompetitionCompetitorStatisticStat
        {
            Id = Guid.NewGuid(),
            CompetitionCompetitorStatisticCategoryId = passing.Id,
            Name = "netPassingYards",
            DisplayName = "Net Passing Yards",
            ShortDisplayName = "YDS",
            Abbreviation = "YDS",
            Value = passYds,
        });
        FootballDataContext.CompetitionCompetitors.Add(new FootballCompetitionCompetitor
        {
            Id = Guid.NewGuid(),
            CompetitionId = stat.CompetitionId,
            FranchiseSeasonId = gainer.Id,
            HomeAway = "away",
        });
        FootballDataContext.CompetitionCompetitorStatistics.Add(stat);
        FootballDataContext.CompetitionCompetitorStatisticCategories.Add(passing);
    }

    [Fact]
    public async Task UnknownPosition_ReturnsValidationFailure()
    {
        var handler = Mocker.CreateInstance<GetAthleteMatchupSummariesQueryHandler>();

        var result = await handler.ExecuteAsync(new GetAthleteMatchupSummariesQuery("PUNTER", 2026, 1));

        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Theory]
    [InlineData(2026, 0)]   // week below range
    [InlineData(2026, 31)]  // week above range
    [InlineData(1999, 1)]   // season before data exists
    [InlineData(2028, 1)]   // season more than a year out (clock fixed at 2026)
    public async Task OutOfRangeNumericInputs_ReturnValidationFailure(int seasonYear, int week)
    {
        var handler = Mocker.CreateInstance<GetAthleteMatchupSummariesQueryHandler>();

        var result = await handler.ExecuteAsync(new GetAthleteMatchupSummariesQuery("QB", seasonYear, week));

        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task KickerRequest_TranslatesKToPk_AndUsesPointsAllowed()
    {
        SeedPositionAndStatus();
        var michigan = SeedFranchiseSeason(2026, "michigan-wolverines", "Michigan Wolverines");
        var opponent = SeedFranchiseSeason(2026, "washington-huskies", "Washington Huskies");
        var kicker = SeedAthleteSeason(Guid.NewGuid(), michigan, "Dominic", "Zvada", KickerPositionId);
        SeedKickingStatDoc(kicker.Id, new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), gamesPlayed: 5);

        SeedWeekContest(home: michigan, away: opponent, seasonYear: 2026, week: 5);

        // Opponent allowed 21 and 17 points in two finalized games -> 19.0/G.
        var somebody = SeedFranchiseSeason(2026, "somebody-state", "Somebody State");
        SeedFinalizedContest(home: opponent, away: somebody, seasonYear: 2026, homeScore: 30, awayScore: 21);
        SeedFinalizedContest(home: somebody, away: opponent, seasonYear: 2026, homeScore: 17, awayScore: 24);

        await FootballDataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetAthleteMatchupSummariesQueryHandler>();
        var result = await handler.ExecuteAsync(new GetAthleteMatchupSummariesQuery("K", 2026, 5));

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Athletes.Should().ContainSingle().Subject;
        row.Position.Should().Be("K"); // UI vocabulary preserved
        row.CurrentSeason!.Stats["fgMade"].Should().Be(9);
        row.CurrentSeason.Stats["fgLong"].Should().Be(56);
        row.CurrentSeason.Stats["xpMade"].Should().Be(14);
        row.OpponentDefPerGame.Should().Be(19.0m); // points allowed, not yards
    }

    [Fact]
    public async Task PostseasonWeekWithSameNumber_DoesNotOverrideRegularSeasonOpponent()
    {
        SeedPositionAndStatus();
        var texas = SeedFranchiseSeason(2026, "texas-longhorns", "Texas Longhorns");
        var regularOpponent = SeedFranchiseSeason(2026, "oklahoma-sooners", "Oklahoma Sooners");
        var bowlOpponent = SeedFranchiseSeason(2026, "georgia-bulldogs", "Georgia Bulldogs");
        SeedAthleteSeason(Guid.NewGuid(), texas, "Arch", "Manning");

        // Same week NUMBER in two phases — only the regular-season game may win.
        SeedWeekContest(home: texas, away: regularOpponent, seasonYear: 2026, week: 1, phaseTypeCode: 2);
        SeedWeekContest(home: texas, away: bowlOpponent, seasonYear: 2026, week: 1, phaseTypeCode: 3);

        await FootballDataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetAthleteMatchupSummariesQueryHandler>();
        var result = await handler.ExecuteAsync(new GetAthleteMatchupSummariesQuery("QB", 2026, 1));

        var row = result.Value.Athletes.Should().ContainSingle().Subject;
        row.OpponentName.Should().Be("Oklahoma Sooners");
    }

    [Fact]
    public async Task MapsStats_ResolvesOpponent_AndAggregatesAllowance()
    {
        SeedPositionAndStatus();
        var texas = SeedFranchiseSeason(2026, "texas-longhorns", "Texas Longhorns");
        var opponent = SeedFranchiseSeason(2026, "oklahoma-sooners", "Oklahoma Sooners");
        var athleteId = Guid.NewGuid();
        var season2026 = SeedAthleteSeason(athleteId, texas, "Arch", "Manning");
        SeedStatDoc(season2026.Id, new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), gamesPlayed: 4, passYds: 1247);

        var texas2025 = SeedFranchiseSeason(2025, "texas-longhorns", "Texas Longhorns", texas.FranchiseId);
        var season2025 = SeedAthleteSeason(athleteId, texas2025, "Arch", "Manning");
        SeedStatDoc(season2025.Id, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), gamesPlayed: 13, passYds: 3163);

        SeedWeekContest(home: opponent, away: texas, seasonYear: 2026, week: 6);

        // Opponent has two played games this season: allowed 300 and 200
        // net passing yards -> 250.0 per game.
        var somebody = SeedFranchiseSeason(2026, "somebody-state", "Somebody State");
        SeedPlayedGame(defense: opponent, gainer: somebody, passYds: 300);
        SeedPlayedGame(defense: opponent, gainer: somebody, passYds: 200);

        await FootballDataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetAthleteMatchupSummariesQueryHandler>();
        var result = await handler.ExecuteAsync(new GetAthleteMatchupSummariesQuery("QB", 2026, 6));

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Athletes.Should().ContainSingle().Subject;
        row.LastName.Should().Be("Manning");
        row.TeamSlug.Should().Be("texas-longhorns");
        row.OpponentName.Should().Be("Oklahoma Sooners");
        row.OpponentSlug.Should().Be("oklahoma-sooners");
        row.OpponentDefPerGame.Should().Be(250.0m);

        row.CurrentSeason.Should().NotBeNull();
        row.CurrentSeason!.SeasonYear.Should().Be(2026);
        row.CurrentSeason.GamesPlayed.Should().Be(4);
        row.CurrentSeason.Stats["passYds"].Should().Be(1247);

        row.PreviousSeason.Should().NotBeNull();
        row.PreviousSeason!.SeasonYear.Should().Be(2025);
        row.PreviousSeason.GamesPlayed.Should().Be(13);
        row.PreviousSeason.Stats["passYds"].Should().Be(3163);
    }

    [Fact]
    public async Task WeekOne_NoCurrentDoc_NullBlock_AndPriorSeasonAllowanceFallback()
    {
        SeedPositionAndStatus();
        var texas = SeedFranchiseSeason(2026, "texas-longhorns", "Texas Longhorns");
        var opponent = SeedFranchiseSeason(2026, "ohio-state-buckeyes", "Ohio State Buckeyes");
        var athleteId = Guid.NewGuid();
        SeedAthleteSeason(athleteId, texas, "Arch", "Manning");
        // No 2026 stat doc — hasn't played.

        SeedWeekContest(home: texas, away: opponent, seasonYear: 2026, week: 1);

        // Opponent's 2026 season has no games; their 2025 season allowed
        // 180 net passing yards in its one recorded game.
        var opponent2025 = SeedFranchiseSeason(2025, "ohio-state-buckeyes", "Ohio State Buckeyes", opponent.FranchiseId);
        var somebody = SeedFranchiseSeason(2025, "somebody-state", "Somebody State");
        SeedPlayedGame(defense: opponent2025, gainer: somebody, passYds: 180);

        await FootballDataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetAthleteMatchupSummariesQueryHandler>();
        var result = await handler.ExecuteAsync(new GetAthleteMatchupSummariesQuery("QB", 2026, 1));

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Athletes.Should().ContainSingle().Subject;
        row.CurrentSeason.Should().BeNull();
        row.OpponentName.Should().Be("Ohio State Buckeyes");
        row.OpponentDefPerGame.Should().Be(180.0m);
    }

    [Fact]
    public async Task DuplicateStatDocs_NewestWins()
    {
        SeedPositionAndStatus();
        var texas = SeedFranchiseSeason(2026, "texas-longhorns", "Texas Longhorns");
        var athleteId = Guid.NewGuid();
        var season = SeedAthleteSeason(athleteId, texas, "Arch", "Manning");
        // Stale doc from an earlier source run, then the corrected one.
        SeedStatDoc(season.Id, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), gamesPlayed: 3, passYds: 900);
        SeedStatDoc(season.Id, new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), gamesPlayed: 4, passYds: 1247);

        await FootballDataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetAthleteMatchupSummariesQueryHandler>();
        var result = await handler.ExecuteAsync(new GetAthleteMatchupSummariesQuery("QB", 2026, 1));

        var row = result.Value.Athletes.Should().ContainSingle().Subject;
        row.CurrentSeason!.GamesPlayed.Should().Be(4);
        row.CurrentSeason.Stats["passYds"].Should().Be(1247);
    }

    [Fact]
    public async Task ByeWeek_OpponentFieldsAreNull()
    {
        SeedPositionAndStatus();
        var texas = SeedFranchiseSeason(2026, "texas-longhorns", "Texas Longhorns");
        SeedAthleteSeason(Guid.NewGuid(), texas, "Arch", "Manning");
        // No contest for the requested week.

        await FootballDataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetAthleteMatchupSummariesQueryHandler>();
        var result = await handler.ExecuteAsync(new GetAthleteMatchupSummariesQuery("QB", 2026, 9));

        var row = result.Value.Athletes.Should().ContainSingle().Subject;
        row.OpponentName.Should().BeNull();
        row.OpponentSlug.Should().BeNull();
        row.OpponentDefPerGame.Should().BeNull();
    }

    [Fact]
    public async Task NflMode_ReturnsAthletes_WithoutRequiringGroupSeasonMap()
    {
        // NFL FranchiseSeasons carry an EMPTY GroupSeasonMap — there is no
        // classification concept — so the FBS filter must be NCAAFB-only.
        SetAppMode(Sport.FootballNfl);
        SeedPositionAndStatus();
        var texans = SeedFranchiseSeason(2026, "houston-texans", "Houston Texans", groupSeasonMap: string.Empty);
        SeedAthleteSeason(Guid.NewGuid(), texans, "C.J.", "Stroud");

        await FootballDataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetAthleteMatchupSummariesQueryHandler>();
        var result = await handler.ExecuteAsync(new GetAthleteMatchupSummariesQuery("QB", 2026, 1));

        result.IsSuccess.Should().BeTrue();
        result.Value.Athletes.Should().ContainSingle()
            .Which.LastName.Should().Be("Stroud");
    }

    [Fact]
    public async Task NonFbsAndInactiveAthletes_AreExcluded()
    {
        SeedPositionAndStatus();
        var fcs = SeedFranchiseSeason(2026, "fcs-school", "FCS School", groupSeasonMap: "NCAAF|NCAA|fcs|southland");
        SeedAthleteSeason(Guid.NewGuid(), fcs, "Fcs", "Quarterback");

        var texas = SeedFranchiseSeason(2026, "texas-longhorns", "Texas Longhorns");
        var inactive = SeedAthleteSeason(Guid.NewGuid(), texas, "Not", "Playing");
        inactive.IsActive = false;

        await FootballDataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetAthleteMatchupSummariesQueryHandler>();
        var result = await handler.ExecuteAsync(new GetAthleteMatchupSummariesQuery("QB", 2026, 1));

        result.IsSuccess.Should().BeTrue();
        result.Value.Athletes.Should().BeEmpty();
    }
}
