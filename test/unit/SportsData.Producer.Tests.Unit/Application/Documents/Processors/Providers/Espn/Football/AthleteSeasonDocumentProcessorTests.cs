using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

/// <summary>
/// Tests for AthleteSeasonDocumentProcessor covering create, update, dependency resolution, and image processing.
/// Optimized to eliminate AutoFixture overhead.
/// </summary>
[Collection("Sequential")]
public class AthleteSeasonDocumentProcessorTests :
    ProducerTestBase<AthleteSeasonDocumentProcessor<FootballDataContext>>
{
    /// <summary>
    /// Validates that when all dependencies exist and a valid AthleteSeason document is provided,
    /// the processor creates a new AthleteSeason entity with all properties mapped correctly.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_CreatesAthleteSeason_WhenAllDependenciesExist()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");
        var dto = json.FromJson<EspnAthleteSeasonDto>();

        var dtoIdentity = generator.Generate(dto!.Ref);
        var franchiseSeasonIdentity = generator.Generate(dto.Team.Ref!);
        var positionIdentity = generator.Generate(dto.Position.Ref!);

        var athleteRef = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/{dto.Id}";
        var athleteIdentity = generator.Generate(athleteRef);

        // OPTIMIZATION: Direct instantiation
        var franchiseSeasonId = franchiseSeasonIdentity.CanonicalId;
        var franchiseSeason = new FranchiseSeason
        {
            Id = franchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TEAM",
            DisplayName = "Team",
            DisplayNameShort = "T",
            Location = "Location",
            Name = "Team",
            Slug = "team",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<FranchiseSeasonExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = franchiseSeasonId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = franchiseSeasonIdentity.CleanUrl,
                    SourceUrlHash = franchiseSeasonIdentity.UrlHash,
                    Value = franchiseSeasonIdentity.UrlHash
                }
            }
        };

        var positionId = Guid.NewGuid();
        var position = new AthletePosition
        {
            Id = positionId,
            Abbreviation = "QB",
            Name = "Quarterback",
            DisplayName = "Quarterback",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<AthletePositionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    AthletePositionId = positionId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = positionIdentity.CleanUrl,
                    SourceUrlHash = positionIdentity.UrlHash,
                    Value = positionIdentity.UrlHash
                }
            }
        };

        var athleteId = athleteIdentity.CanonicalId;
        var athlete = new FootballAthlete
        {
            Id = athleteId,
            LastName = dto.LastName,
            FirstName = dto.FirstName,
            DisplayName = $"{dto.FirstName} {dto.LastName}",
            ShortName = dto.LastName,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var athleteExternalId = new AthleteExternalId
        {
            Id = Guid.NewGuid(),
            AthleteId = athleteId,
            Provider = SourceDataProvider.Espn,
            SourceUrl = athleteRef,
            SourceUrlHash = athleteIdentity.UrlHash,
            Value = dto.Id
        };

        await FootballDataContext.AthletePositions.AddAsync(position);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.Athletes.AddAsync(athlete);
        await FootballDataContext.AthleteExternalIds.AddAsync(athleteExternalId);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2024)
            .With(x => x.DocumentType, DocumentType.AthleteSeason)
            .With(x => x.Document, json)
            .With(x => x.UrlHash, dtoIdentity.UrlHash)
            .Without(x => x.ParentId)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var entity = await FootballDataContext.AthleteSeasons
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync();

        entity.Should().NotBeNull();
        entity!.Id.Should().Be(dtoIdentity.CanonicalId);
        entity.AthleteId.Should().Be(athlete.Id);
        entity.PositionId.Should().Be(position.Id);
        entity.FranchiseSeasonId.Should().Be(franchiseSeason.Id);
        entity.FirstName.Should().Be(dto.FirstName);
        entity.LastName.Should().Be(dto.LastName);
        entity.DisplayName.Should().Be(dto.DisplayName);
        entity.Jersey.Should().Be(dto.Jersey);

        // Verify headshot image request was published (EspnFootballNcaaAthleteSeason.json has headshot)
        // Note: ParentEntityId is the Athlete ID (career-level), not AthleteSeason ID
        bus.Verify(x => x.Publish(
            It.Is<ProcessImageRequest>(e => 
                e.ParentEntityId == athlete.Id &&
                e.DocumentType == DocumentType.AthleteImage),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify statistics request was published (EspnFootballNcaaAthleteSeason.json has statistics)
        bus.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.AthleteSeasonStatistics
                && e.ParentId == entity.Id.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Validates that when an AthleteSeason already exists, the processor updates all properties
    /// and republishes image and statistics requests.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_UpdatesAthleteSeason_WhenEntityAlreadyExists()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var now = DateTime.UtcNow;
        var dateTimeProvider = Mocker.GetMock<SportsData.Core.Common.IDateTimeProvider>();
        dateTimeProvider.Setup(x => x.UtcNow()).Returns(now);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");
        var dto = json.FromJson<EspnAthleteSeasonDto>();

        var dtoIdentity = generator.Generate(dto!.Ref);
        var franchiseSeasonIdentity = generator.Generate(dto.Team.Ref!);
        var positionIdentity = generator.Generate(dto.Position.Ref!);

        var athleteRef = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/{dto.Id}";
        var athleteIdentity = generator.Generate(athleteRef);

        // OPTIMIZATION: Direct instantiation
        var positionId = Guid.NewGuid();
        var position = new AthletePosition
        {
            Id = positionId,
            Abbreviation = "QB",
            Name = "Quarterback",
            DisplayName = "Quarterback",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<AthletePositionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    AthletePositionId = positionId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = positionIdentity.CleanUrl,
                    SourceUrlHash = positionIdentity.UrlHash,
                    Value = positionIdentity.UrlHash
                }
            }
        };

        var franchiseSeasonId = franchiseSeasonIdentity.CanonicalId;
        var franchiseSeason = new FranchiseSeason
        {
            Id = franchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TEAM",
            DisplayName = "Team",
            DisplayNameShort = "T",
            Location = "Location",
            Name = "Team",
            Slug = "team",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var franchiseSeasonExternalId = new FranchiseSeasonExternalId
        {
            Id = Guid.NewGuid(),
            FranchiseSeasonId = franchiseSeasonId,
            Provider = SourceDataProvider.Espn,
            SourceUrl = franchiseSeasonIdentity.CleanUrl,
            SourceUrlHash = franchiseSeasonIdentity.UrlHash,
            Value = franchiseSeasonIdentity.UrlHash
        };

        var athleteId = athleteIdentity.CanonicalId;
        var athlete = new FootballAthlete
        {
            Id = athleteId,
            FirstName = "Test",
            LastName = "Player",
            DisplayName = "Test Player",
            ShortName = "Player",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // Create existing AthleteSeason with old data
        var existingAthleteSeason = new FootballAthleteSeason
        {
            Id = dtoIdentity.CanonicalId,
            AthleteId = athlete.Id,
            FranchiseSeasonId = franchiseSeason.Id,
            PositionId = position.Id,
            DisplayName = "Old Display Name",
            Jersey = "99",
            ExperienceYears = 1,
            FirstName = "Old",
            LastName = "Name",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.AthletePositions.AddAsync(position);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.FranchiseSeasonExternalIds.AddAsync(franchiseSeasonExternalId);
        await FootballDataContext.Athletes.AddAsync(athlete);
        await FootballDataContext.AthleteSeasons.AddAsync(existingAthleteSeason);
        await FootballDataContext.SaveChangesAsync();

        var command = new ProcessDocumentCommand(
            sourceDataProvider: SourceDataProvider.Espn,
            sport: Sport.FootballNcaa,
            season: 2024,
            documentType: DocumentType.AthleteSeason,
            document: json,
            messageId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(),
            parentId: null,
            sourceUri: new Uri(dtoIdentity.CleanUrl),
            urlHash: dtoIdentity.UrlHash,
            includeLinkedDocumentTypes: null);

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var updatedEntity = await FootballDataContext.AthleteSeasons.FirstOrDefaultAsync(x => x.Id == dtoIdentity.CanonicalId);

        updatedEntity.Should().NotBeNull();
        updatedEntity!.DisplayName.Should().Be(dto.DisplayName, "DisplayName should be updated");
        updatedEntity.Jersey.Should().Be(dto.Jersey, "Jersey should be updated");
        updatedEntity.ExperienceYears.Should().Be(dto.Experience.Years, "Experience should be updated");
        updatedEntity.ModifiedUtc.Should().Be(now);

        // Verify image request was published
        // Note: Image request uses Athlete ID as ParentEntityId (career-level images)
        bus.Verify(x => x.Publish(
            It.Is<ProcessImageRequest>(e =>
                e.ParentEntityId == athlete.Id &&
                e.DocumentType == DocumentType.AthleteImage),
            It.IsAny<CancellationToken>()), Times.Once);

        // Note: Statistics requests verified separately in ProcessAsync_PublishesStatisticsRequest_WhenStatisticsRefExists

        // Verify only 1 AthleteSeason exists (no duplicates)
        (await FootballDataContext.AthleteSeasons.CountAsync()).Should().Be(1);
    }

    /// <summary>
    /// Validates that when the Athlete does not exist, a DocumentRequested event is published
    /// and the processor handles the retry scenario (ExternalDocumentNotSourcedException is caught).
    /// </summary>
    [Fact]
    public async Task ProcessAsync_RequestsAthlete_WhenAthleteNotFound()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        // Enable dependency requests for this test (override mode)
        var config = new SportsData.Producer.Config.DocumentProcessingConfig { EnableDependencyRequests = true };
        Mocker.Use(config);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2024)
            .With(x => x.DocumentType, DocumentType.AthleteSeason)
            .With(x => x.Document, json)
            .With(x => x.AttemptCount, 0)
            .Without(x => x.ParentId)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert - Processor catches ExternalDocumentNotSourcedException and publishes DocumentRequested + retry DocumentCreated
        bus.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.Athlete),
            It.IsAny<CancellationToken>()), Times.Once);

        bus.Verify(x => x.Publish(
            It.Is<DocumentCreated>(e => e.AttemptCount == 1),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        (await FootballDataContext.AthleteSeasons.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// Validates that when the FranchiseSeason (Team) does not exist, a DocumentRequested event
    /// is published and the processor handles the retry scenario.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_RequestsFranchiseSeason_WhenTeamNotFound()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        // Enable dependency requests for this test (override mode)
        var config = new SportsData.Producer.Config.DocumentProcessingConfig { EnableDependencyRequests = true };
        Mocker.Use(config);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");
        var dto = json.FromJson<EspnAthleteSeasonDto>();

        var athleteRef = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/{dto!.Id}";
        var athleteIdentity = generator.Generate(athleteRef);

        // OPTIMIZATION: Direct instantiation - Create only the Athlete (no FranchiseSeason)
        var athleteId = athleteIdentity.CanonicalId;
        var athlete = new FootballAthlete
        {
            Id = athleteId,
            FirstName = "Test",
            LastName = "Player",
            DisplayName = "Test Player",
            ShortName = "Player",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.Athletes.AddAsync(athlete);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2024)
            .With(x => x.DocumentType, DocumentType.AthleteSeason)
            .With(x => x.Document, json)
            .With(x => x.AttemptCount, 0)
            .Without(x => x.ParentId)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        bus.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.TeamSeason),
            It.IsAny<CancellationToken>()), Times.Once);

        bus.Verify(x => x.Publish(
            It.Is<DocumentCreated>(e => e.AttemptCount == 1),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        (await FootballDataContext.AthleteSeasons.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// Validates that when the AthletePosition does not exist, a DocumentRequested event
    /// is published and the processor handles the retry scenario.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_RequestsPosition_WhenPositionNotFound()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        // Enable dependency requests for this test (override mode)
        var config = new SportsData.Producer.Config.DocumentProcessingConfig { EnableDependencyRequests = true };
        Mocker.Use(config);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");
        var dto = json.FromJson<EspnAthleteSeasonDto>();

        var franchiseSeasonIdentity = generator.Generate(dto!.Team.Ref!);
        var athleteRef = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/{dto.Id}";
        var athleteIdentity = generator.Generate(athleteRef);

        // OPTIMIZATION: Direct instantiation - Create Athlete and FranchiseSeason (but no Position)
        var franchiseSeasonId = franchiseSeasonIdentity.CanonicalId;
        var franchiseSeason = new FranchiseSeason
        {
            Id = franchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TEAM",
            DisplayName = "Team",
            DisplayNameShort = "T",
            Location = "Location",
            Name = "Team",
            Slug = "team",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var athleteId = athleteIdentity.CanonicalId;
        var athlete = new FootballAthlete
        {
            Id = athleteId,
            FirstName = "Test",
            LastName = "Player",
            DisplayName = "Test Player",
            ShortName = "Player",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.Athletes.AddAsync(athlete);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2024)
            .With(x => x.DocumentType, DocumentType.AthleteSeason)
            .With(x => x.Document, json)
            .With(x => x.AttemptCount, 0)
            .Without(x => x.ParentId)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        bus.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.AthletePosition),
            It.IsAny<CancellationToken>()), Times.Once);

        bus.Verify(x => x.Publish(
            It.Is<DocumentCreated>(e => e.AttemptCount == 1),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        (await FootballDataContext.AthleteSeasons.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// Validates that when the document JSON is null or cannot be deserialized,
    /// the processor logs an error and does not create any entities.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_DoesNothing_WhenDocumentIsNull()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor<FootballDataContext>>();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2024)
            .With(x => x.DocumentType, DocumentType.AthleteSeason)
            .With(x => x.Document, "null")
            .Without(x => x.ParentId)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        (await FootballDataContext.AthleteSeasons.CountAsync()).Should().Be(0);
        bus.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Validates that when the document's Ref property is null, the processor logs an error
    /// and does not process the document.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_DoesNothing_WhenRefIsNull()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor<FootballDataContext>>();

        // Create a DTO with null Ref
        var invalidJson = "{\"id\":\"123\",\"firstName\":\"Test\",\"lastName\":\"Player\"}";

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2024)
            .With(x => x.DocumentType, DocumentType.AthleteSeason)
            .With(x => x.Document, invalidJson)
            .Without(x => x.ParentId)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        (await FootballDataContext.AthleteSeasons.CountAsync()).Should().Be(0);
        bus.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Validates that ProcessImageRequest is published with correct parameters when headshot exists.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_PublishesImageRequest_WhenHeadshotExists()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");
        var dto = json.FromJson<EspnAthleteSeasonDto>();

        // Setup all dependencies
        var dtoIdentity = generator.Generate(dto!.Ref);
        var franchiseSeasonIdentity = generator.Generate(dto.Team.Ref!);
        var positionIdentity = generator.Generate(dto.Position.Ref!);
        var athleteRef = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/{dto.Id}";
        var athleteIdentity = generator.Generate(athleteRef);

        // OPTIMIZATION: Direct instantiation
        var positionId = Guid.NewGuid();
        var position = new AthletePosition
        {
            Id = positionId,
            Abbreviation = "QB",
            Name = "Quarterback",
            DisplayName = "Quarterback",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<AthletePositionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    AthletePositionId = positionId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = positionIdentity.CleanUrl,
                    SourceUrlHash = positionIdentity.UrlHash,
                    Value = positionIdentity.UrlHash
                }
            }
        };

        var franchiseSeasonId = franchiseSeasonIdentity.CanonicalId;
        var franchiseSeason = new FranchiseSeason
        {
            Id = franchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TEAM",
            DisplayName = "Team",
            DisplayNameShort = "T",
            Location = "Location",
            Name = "Team",
            Slug = "team",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var athleteId = athleteIdentity.CanonicalId;
        var athlete = new FootballAthlete
        {
            Id = athleteId,
            FirstName = "Test",
            LastName = "Player",
            DisplayName = "Test Player",
            ShortName = "Player",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.AthletePositions.AddAsync(position);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.Athletes.AddAsync(athlete);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2024)
            .With(x => x.DocumentType, DocumentType.AthleteSeason)
            .With(x => x.Document, json)
            .Without(x => x.ParentId)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        bus.Verify(x => x.Publish(
            It.Is<ProcessImageRequest>(e =>
                e.Url == dto.Headshot.Href &&
                e.ParentEntityId == athleteId &&
                e.Sport == Sport.FootballNcaa &&
                e.DocumentType == DocumentType.AthleteImage),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Validates that DocumentRequested for statistics is published when statistics ref exists.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_PublishesStatisticsRequest_WhenStatisticsRefExists()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");
        var dto = json.FromJson<EspnAthleteSeasonDto>();

        // Setup all dependencies
        var dtoIdentity = generator.Generate(dto!.Ref);
        var franchiseSeasonIdentity = generator.Generate(dto.Team.Ref!);
        var positionIdentity = generator.Generate(dto.Position.Ref!);
        var athleteRef = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/{dto.Id}";
        var athleteIdentity = generator.Generate(athleteRef);

        // OPTIMIZATION: Direct instantiation
        var positionId = Guid.NewGuid();
        var position = new AthletePosition
        {
            Id = positionId,
            Abbreviation = "QB",
            Name = "Quarterback",
            DisplayName = "Quarterback",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<AthletePositionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    AthletePositionId = positionId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = positionIdentity.CleanUrl,
                    SourceUrlHash = positionIdentity.UrlHash,
                    Value = positionIdentity.UrlHash
                }
            }
        };

        var franchiseSeasonId = franchiseSeasonIdentity.CanonicalId;
        var franchiseSeason = new FranchiseSeason
        {
            Id = franchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TEAM",
            DisplayName = "Team",
            DisplayNameShort = "T",
            Location = "Location",
            Name = "Team",
            Slug = "team",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var athleteId = athleteIdentity.CanonicalId;
        var athlete = new FootballAthlete
        {
            Id = athleteId,
            FirstName = "Test",
            LastName = "Player",
            DisplayName = "Test Player",
            ShortName = "Player",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.AthletePositions.AddAsync(position);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.Athletes.AddAsync(athlete);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2024)
            .With(x => x.DocumentType, DocumentType.AthleteSeason)
            .With(x => x.Document, json)
            .Without(x => x.ParentId)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        bus.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e =>
                e.DocumentType == DocumentType.AthleteSeasonStatistics &&
                e.ParentId == dtoIdentity.CanonicalId.ToString() &&
                e.Sport == Sport.FootballNcaa),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Validates that when the AthleteSeason DTO has a null Team.Ref (placeholder/negative-ID athlete),
    /// the processor creates the AthleteSeason with a null FranchiseSeasonId and does NOT publish
    /// a dependency request for TeamSeason or throw ExternalDocumentNotSourcedException.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_CreatesAthleteSeason_WithNullFranchiseSeasonId_WhenTeamRefIsNull()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeasonNoTeam.json");
        var dto = json.FromJson<EspnAthleteSeasonDto>();

        var dtoIdentity = generator.Generate(dto!.Ref);
        var positionIdentity = generator.Generate(dto.Position.Ref!);

        // Placeholder athletes use a negative ID in the ESPN URL
        var athleteRef = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/{dto.Id}";
        var athleteIdentity = generator.Generate(athleteRef);

        var positionId = Guid.NewGuid();
        var position = new AthletePosition
        {
            Id = positionId,
            Abbreviation = "RB",
            Name = "Running Back",
            DisplayName = "Running Back",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<AthletePositionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    AthletePositionId = positionId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = positionIdentity.CleanUrl,
                    SourceUrlHash = positionIdentity.UrlHash,
                    Value = positionIdentity.UrlHash
                }
            }
        };

        var athleteId = athleteIdentity.CanonicalId;
        var athlete = new FootballAthlete
        {
            Id = athleteId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            DisplayName = dto.DisplayName,
            ShortName = dto.ShortName,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // Intentionally no FranchiseSeason seeded — placeholder athletes have no team
        await FootballDataContext.AthletePositions.AddAsync(position);
        await FootballDataContext.Athletes.AddAsync(athlete);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2023)
            .With(x => x.DocumentType, DocumentType.AthleteSeason)
            .With(x => x.Document, json)
            .Without(x => x.ParentId)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert — entity created without a FranchiseSeasonId
        var entity = await FootballDataContext.AthleteSeasons
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync();

        entity.Should().NotBeNull();
        entity!.Id.Should().Be(dtoIdentity.CanonicalId);
        entity.AthleteId.Should().Be(athlete.Id);
        entity.FranchiseSeasonId.Should().BeNull("placeholder athletes have no team ref");
        entity.PositionId.Should().Be(positionId);

        // No TeamSeason dependency request should be raised for a null Team.Ref
        bus.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.TeamSeason),
            It.IsAny<CancellationToken>()), Times.Never);

        // No retry event should be published — processing should complete successfully
        bus.Verify(x => x.Publish(
            It.IsAny<DocumentCreated>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Never);

        (await FootballDataContext.AthleteSeasons.CountAsync()).Should().Be(1);
    }
}


