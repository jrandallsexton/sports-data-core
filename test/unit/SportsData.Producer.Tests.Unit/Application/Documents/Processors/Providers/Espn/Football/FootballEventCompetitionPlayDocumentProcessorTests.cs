#nullable enable

using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

/// <summary>
/// Tests for FootballEventCompetitionPlayDocumentProcessor.
/// Optimized to eliminate AutoFixture overhead.
/// </summary>
[Collection("Sequential")]
public class FootballEventCompetitionPlayDocumentProcessorTests : ProducerTestBase<FootballDataContext>
{
    private const string PlayUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/plays/401628334123";

    // Fixed "now" for every CreatedUtc in this file. Per CLAUDE.md, tests
    // route timestamps through IDateTimeProvider so seeded entities are
    // deterministic. Matches the pattern in MatchupScheduleProcessorTests.
    private static readonly DateTime FixedUtcNow = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public FootballEventCompetitionPlayDocumentProcessorTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedUtcNow);
    }

    private DateTime UtcNow() => Mocker.Get<IDateTimeProvider>().UtcNow();

    /// <summary>
    /// Seed an in-progress FootballCompetitionStatus row for the given
    /// competition. The play processor's base now throws when no status
    /// row exists (treats it as a transient race with the status
    /// processor and lets MassTransit retry); these tests aren't
    /// exercising that path, so they need a status row to proceed.
    /// </summary>
    private async Task SeedInProgressStatusAsync(Guid competitionId)
    {
        await FootballDataContext.Set<FootballCompetitionStatus>().AddAsync(new FootballCompetitionStatus
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            IsCompleted = false,
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid()
        });
        await FootballDataContext.SaveChangesAsync();
    }

    private ProcessDocumentCommand CreateCommand(string jsonFile, string? parentId = null)
    {
        var generator = new ExternalRefIdentityGenerator();
        return Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, jsonFile)
            .With(x => x.DocumentType, DocumentType.EventCompetitionPlay)
            .With(x => x.SeasonYear, 2024)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, parentId ?? Guid.NewGuid().ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .With(x => x.UrlHash, generator.Generate(PlayUrl).UrlHash)
            .Create();
    }

    [Fact]
    public async Task WhenJsonIsValid_CanDeserialize()
    {
        // arrange
        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_Debug.json");
        var play = json.FromJson<EspnFootballEventCompetitionPlayDto>();

        // act


        // assert
        play.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenPlayCollectionIsProvided_ScoreCanBeCalculate()
    {
        // arrange
        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlays.json");
        var plays = json.FromJson<List<EspnFootballEventCompetitionPlayDto>>();

        // act
        var scoringPlays = plays!.Where(x => x.ScoringPlay).ToList();

        // assert
        scoringPlays.Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AsFootballEntity_CapturesScoringTypePointAfterAttemptAndWallclock()
    {
        // Real fixture: a touchdown play carries scoringType + pointAfterAttempt +
        // wallclock — all previously dropped by the mapper.
        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlays.json");
        var plays = json.FromJson<List<EspnFootballEventCompetitionPlayDto>>()!;
        var td = plays.First(p => p.ScoringType?.Abbreviation == "TD" && p.PointAfterAttempt != null);

        var entity = td.AsFootballEntity(
            new ExternalRefIdentityGenerator(),
            correlationId: Guid.NewGuid(),
            competitionId: Guid.NewGuid(),
            driveId: null,
            startFranchiseSeasonId: null,
            endFranchiseSeasonId: null);

        entity.ScoringTypeName.Should().Be("touchdown");
        entity.ScoringTypeDisplayName.Should().Be("Touchdown");
        entity.ScoringTypeAbbreviation.Should().Be("TD");
        entity.PointAfterAttemptId.Should().Be(61);
        entity.PointAfterAttemptText.Should().Be("Extra Point Good");
        entity.PointAfterAttemptAbbreviation.Should().Be("Extra Point Good");
        entity.PointAfterAttemptValue.Should().Be(1);
        entity.Wallclock.Should().Be(td.Wallclock);
    }

    [Fact]
    public async Task AsFootballEntity_NonScoringPlay_LeavesScoringFieldsNull()
    {
        // A non-scoring play has no scoringType / pointAfterAttempt.
        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlays.json");
        var plays = json.FromJson<List<EspnFootballEventCompetitionPlayDto>>()!;
        var nonScoring = plays.First(p => p.ScoringType == null && p.PointAfterAttempt == null);

        var entity = nonScoring.AsFootballEntity(
            new ExternalRefIdentityGenerator(),
            correlationId: Guid.NewGuid(),
            competitionId: Guid.NewGuid(),
            driveId: null,
            startFranchiseSeasonId: null,
            endFranchiseSeasonId: null);

        entity.ScoringTypeName.Should().BeNull();
        entity.ScoringTypeAbbreviation.Should().BeNull();
        entity.PointAfterAttemptId.Should().BeNull();
        entity.PointAfterAttemptValue.Should().BeNull();
    }

    [Fact]
    public async Task WhenProcessingKickoffReturnPlay_ShouldCreatePlayWithCorrectData()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var competitionId = Guid.NewGuid();
        
        // OPTIMIZATION: Direct instantiation
        var competition = new FootballCompetition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = UtcNow(),
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.SaveChangesAsync();

        await SeedInProgressStatusAsync(competitionId);

        // Setup team franchise seasons for both teams
        var startTeamId = Guid.NewGuid();
        var startTeamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99";
        
        // OPTIMIZATION: Direct instantiation
        var startTeam = new FranchiseSeason
        {
            Id = startTeamId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TEAM1",
            DisplayName = "Team 1",
            DisplayNameShort = "T1",
            Location = "Location",
            Name = "Team 1",
            Slug = "team-1",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<FranchiseSeasonExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = startTeamId,
                    Provider = SourceDataProvider.Espn,
                    Value = generator.Generate(startTeamUrl).UrlHash,
                    SourceUrl = startTeamUrl,
                    SourceUrlHash = generator.Generate(startTeamUrl).UrlHash,
                    CreatedBy = Guid.NewGuid()
                }
            }
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(startTeam);
        await FootballDataContext.SaveChangesAsync();

        var returnTeamId = Guid.NewGuid();
        var returnTeamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/30";
        
        // OPTIMIZATION: Direct instantiation
        var returnTeam = new FranchiseSeason
        {
            Id = returnTeamId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TEAM2",
            DisplayName = "Team 2",
            DisplayNameShort = "T2",
            Location = "Location",
            Name = "Team 2",
            Slug = "team-2",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<FranchiseSeasonExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = returnTeamId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = returnTeamUrl,
                    SourceUrlHash = generator.Generate(returnTeamUrl).UrlHash,
                    Value = generator.Generate(returnTeamUrl).UrlHash,
                    CreatedBy = Guid.NewGuid()
                }
            }
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(returnTeam);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<FootballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json, competitionId.ToString());

        // act
        await sut.ProcessAsync(command);

        // assert
        var play = await FootballDataContext.CompetitionPlays
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();
        play!.ExternalIds.Should().NotBeEmpty();
        play.Type.Should().Be(PlayType.KickoffReturnOffense);
        play.StatYardage.Should().Be(16);
        play.StartYardLine.Should().Be(65);
        play.EndYardLine.Should().Be(23);
        play.StartDown.Should().Be(1);
        play.StartFranchiseSeasonId.Should().Be(returnTeamId);
        play.EndFranchiseSeasonId.Should().Be(startTeamId);
        play.ClockValue.Should().Be(900);
        play.ClockDisplayValue.Should().Be("15:00");
        play.Text.Should().Be("Michael Lantz kickoff for 58 yds , Zavion Thomas return for 16 yds to the LSU 23");
    }

    [Fact]
    public async Task WhenEntityDoesNotExist_ShouldCreatePlayWithCorrectData()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var competitionId = Guid.NewGuid();
        
        // OPTIMIZATION: Direct instantiation
        var competition = new FootballCompetition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = UtcNow(),
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.SaveChangesAsync();

        await SeedInProgressStatusAsync(competitionId);

        var startTeamId = Guid.NewGuid();
        var startTeamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/30";
        
        // OPTIMIZATION: Direct instantiation
        var franchiseSeason = new FranchiseSeason
        {
            Id = startTeamId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TEAM",
            DisplayName = "Team",
            DisplayNameShort = "T",
            Location = "Location",
            Name = "Team",
            Slug = "team",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        // OPTIMIZATION: Direct instantiation
        var externalId = new FranchiseSeasonExternalId
        {
            Id = Guid.NewGuid(),
            FranchiseSeasonId = startTeamId,
            Provider = SourceDataProvider.Espn,
            SourceUrl = startTeamUrl,
            SourceUrlHash = generator.Generate(startTeamUrl).UrlHash,
            Value = generator.Generate(startTeamUrl).UrlHash,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.FranchiseSeasonExternalIds.AddAsync(externalId);

        // Add the FranchiseSeason for dto.Team.Ref
        var teamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99?lang=en";
        var mainFranchiseSeasonId = Guid.NewGuid();
        
        // OPTIMIZATION: Direct instantiation
        var mainFranchiseSeason = new FranchiseSeason
        {
            Id = mainFranchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "MAIN",
            DisplayName = "Main Team",
            DisplayNameShort = "MT",
            Location = "Location",
            Name = "Main Team",
            Slug = "main-team",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<FranchiseSeasonExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = mainFranchiseSeasonId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = new Uri(teamUrl).ToCleanUrl(),
                    SourceUrlHash = generator.Generate(teamUrl).UrlHash,
                    Value = generator.Generate(teamUrl).UrlHash
                }
            }
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(mainFranchiseSeason);

        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<FootballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json, competitionId.ToString());

        // act
        await sut.ProcessAsync(command);

        // assert
        var play = await FootballDataContext.CompetitionPlays
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();
        play!.ExternalIds.Should().NotBeEmpty();
        play.Competition.Should().NotBeNull();
        play.Competition!.Id.Should().Be(competitionId);
    }

    [Fact]
    public async Task WhenEntityExists_FullRemap_RefreshesFields_PreservesIdentityAuditAndExternalIds()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var competitionId = Guid.NewGuid();
        var competition = new FootballCompetition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = UtcNow(),
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.SaveChangesAsync();
        await SeedInProgressStatusAsync(competitionId);

        // Seed an EXISTING play keyed by the fixture ref's canonical id, with a
        // deliberately stale Text and preserved audit columns so the remap is
        // observable. The processor finds it by canonical id and takes the
        // update path. Derive the id from the FIXTURE'S ref (not PlayUrl, which is
        // a different play) so the lookup matches.
        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var playRef = json.FromJson<EspnFootballEventCompetitionPlayDto>()!.Ref;
        var canonicalId = generator.Generate(playRef).CanonicalId;
        var urlHash = generator.Generate(playRef).UrlHash;
        var originalCreatedUtc = new DateTime(2020, 5, 5, 0, 0, 0, DateTimeKind.Utc);
        var originalCreatedBy = Guid.NewGuid();

        var play = new FootballCompetitionPlay
        {
            Id = canonicalId,
            CompetitionId = competitionId,
            EspnId = "stale-espn-id",
            SequenceNumber = "0",
            Text = "STALE TEXT",
            TypeId = "0",
            CreatedUtc = originalCreatedUtc,
            CreatedBy = originalCreatedBy
        };
        var externalId = new CompetitionPlayExternalId
        {
            Id = Guid.NewGuid(),
            CompetitionPlayId = canonicalId,
            Provider = SourceDataProvider.Espn,
            Value = urlHash,
            SourceUrl = playRef.AbsoluteUri,
            SourceUrlHash = urlHash,
            CreatedBy = Guid.NewGuid()
        };
        play.ExternalIds.Add(externalId);
        await FootballDataContext.CompetitionPlays.AddAsync(play);
        await FootballDataContext.SaveChangesAsync();
        var originalExternalIdRowId = externalId.Id;

        // Fresh DI scope per message in production.
        FootballDataContext.ChangeTracker.Clear();

        var sut = Mocker.CreateInstance<FootballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        // act — reprocess the same play (update path).
        await sut.ProcessAsync(CreateCommand(json, competitionId.ToString()));

        // assert
        var updated = await FootballDataContext.CompetitionPlays
            .AsNoTracking()
            .Include(x => x.ExternalIds)
            .FirstAsync(x => x.Id == canonicalId);

        // Full remap refreshed the field (previously the update path left it stale).
        updated.Text.Should().NotBe("STALE TEXT");
        // Identity + audit preserved.
        updated.Id.Should().Be(canonicalId);
        updated.CreatedUtc.Should().Be(originalCreatedUtc);
        updated.CreatedBy.Should().Be(originalCreatedBy);
        // ExternalIds is a navigation — SetValues touches scalars only, so the
        // original row is untouched (not duplicated or replaced).
        updated.ExternalIds.Should().ContainSingle()
            .Which.Id.Should().Be(originalExternalIdRowId);
    }

    [Fact(Skip = "log not throw")]
    public async Task WhenSeasonMissing_ThrowsException()
    {
        // arrange
        var sut = Mocker.CreateInstance<FootballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json);
        command = new ProcessDocumentCommand(
            command.SourceDataProvider,
            command.Sport,
            null, // Set Season to null
            command.DocumentType,
            command.Document,
            command.MessageId,
            command.CorrelationId,
            command.ParentId,
            command.SourceUri,
            command.UrlHash);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessAsync(command));
    }

    [Fact(Skip="log not throw")]
    public async Task WhenParentIdInvalid_ThrowsException()
    {
        // arrange
        var sut = Mocker.CreateInstance<FootballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json);
        command = new ProcessDocumentCommand(
            command.SourceDataProvider,
            command.Sport,
            command.SeasonYear,
            command.DocumentType,
            command.Document,
            command.MessageId,
            command.CorrelationId,
            "invalid-guid",
            command.SourceUri,
            command.UrlHash);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessAsync(command));
    }

    [Fact(Skip = "log not throw")]
    public async Task WhenParentIdMissing_ThrowsException()
    {
        // arrange
        var sut = Mocker.CreateInstance<FootballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json);
        command = new ProcessDocumentCommand(
            command.SourceDataProvider,
            command.Sport,
            command.SeasonYear,
            command.DocumentType,
            command.Document,
            command.MessageId,
            command.CorrelationId,
            null,
            command.SourceUri,
            command.UrlHash);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessAsync(command));
    }
}