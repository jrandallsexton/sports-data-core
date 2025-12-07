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
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

/// <summary>
/// Tests for AthleteSeasonDocumentProcessor covering create, update, dependency resolution, and image processing.
/// </summary>
public class AthleteSeasonDocumentProcessorTests :
    ProducerTestBase<AthleteSeasonDocumentProcessor>
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
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");
        var dto = json.FromJson<EspnAthleteSeasonDto>();

        var dtoIdentity = generator.Generate(dto!.Ref);
        var franchiseSeasonIdentity = generator.Generate(dto.Team.Ref!);
        var positionIdentity = generator.Generate(dto.Position.Ref!);

        var athleteRef = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/{dto.Id}";
        var athleteIdentity = generator.Generate(athleteRef);

        var franchise = Fixture.Build<Franchise>()
            .WithAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Seasons, new List<FranchiseSeason>())
            .Create();

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .WithAutoProperties()
            .With(x => x.Id, franchiseSeasonIdentity.CanonicalId)
            .With(x => x.FranchiseId, franchise.Id)
            .With(x => x.SeasonYear, 2024)
            .With(x => x.ExternalIds, new List<FranchiseSeasonExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = franchiseSeasonIdentity.CleanUrl,
                    SourceUrlHash = franchiseSeasonIdentity.UrlHash,
                    Value = franchiseSeasonIdentity.UrlHash
                }
            })
            .Create();

        var position = Fixture.Build<AthletePosition>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Abbreviation, "QB")
            .With(x => x.ExternalIds, new List<AthletePositionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = positionIdentity.CleanUrl,
                    SourceUrlHash = positionIdentity.UrlHash,
                    Value = positionIdentity.UrlHash
                }
            })
            .Create();

        var athlete = Fixture.Build<FootballAthlete>()
            .WithAutoProperties()
            .With(x => x.Id, athleteIdentity.CanonicalId)
            .With(x => x.LastName, dto.LastName)
            .With(x => x.FirstName, dto.FirstName)
            .With(x => x.Seasons, new List<AthleteSeason>())
            .Create();

        var athleteExternalId = new AthleteExternalId
        {
            Id = Guid.NewGuid(),
            AthleteId = athleteIdentity.CanonicalId,
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
        bus.Verify(x => x.Publish(
            It.Is<ProcessImageRequest>(e => e.ParentEntityId == entity.Id),
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

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");
        var dto = json.FromJson<EspnAthleteSeasonDto>();

        var dtoIdentity = generator.Generate(dto!.Ref);
        var franchiseSeasonIdentity = generator.Generate(dto.Team.Ref!);
        var positionIdentity = generator.Generate(dto.Position.Ref!);

        var athleteRef = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/{dto.Id}";
        var athleteIdentity = generator.Generate(athleteRef);

        // Setup dependencies
        var position = Fixture.Build<AthletePosition>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.ExternalIds, new List<AthletePositionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = positionIdentity.CleanUrl,
                    SourceUrlHash = positionIdentity.UrlHash,
                    Value = positionIdentity.UrlHash
                }
            })
            .Create();

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .WithAutoProperties()
            .With(x => x.Id, franchiseSeasonIdentity.CanonicalId)
            .With(x => x.SeasonYear, 2024)
            .Create();

        var athlete = Fixture.Build<FootballAthlete>()
            .WithAutoProperties()
            .With(x => x.Id, athleteIdentity.CanonicalId)
            .With(x => x.Seasons, new List<AthleteSeason>())
            .Create();

        // Create existing AthleteSeason with old data
        var existingAthleteSeason = Fixture.Build<FootballAthleteSeason>()
            .With(x => x.Id, dtoIdentity.CanonicalId)
            .With(x => x.AthleteId, athlete.Id)
            .With(x => x.FranchiseSeasonId, franchiseSeason.Id)
            .With(x => x.PositionId, position.Id)
            .With(x => x.DisplayName, "Old Display Name")
            .With(x => x.Jersey, "99")
            .With(x => x.ExperienceYears, 1)
            .Create();

        await FootballDataContext.AthletePositions.AddAsync(position);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.Athletes.AddAsync(athlete);
        await FootballDataContext.AthleteSeasons.AddAsync(existingAthleteSeason);
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
        var updatedEntity = await FootballDataContext.AthleteSeasons.FirstOrDefaultAsync(x => x.Id == dtoIdentity.CanonicalId);

        updatedEntity.Should().NotBeNull();
        updatedEntity!.DisplayName.Should().Be(dto.DisplayName, "DisplayName should be updated");
        updatedEntity.Jersey.Should().Be(dto.Jersey, "Jersey should be updated");
        updatedEntity.ExperienceYears.Should().Be(dto.Experience.Years, "Experience should be updated");
        updatedEntity.ModifiedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify image and statistics requests were published
        bus.Verify(x => x.Publish(It.IsAny<ProcessImageRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        bus.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.AthleteSeasonStatistics),
            It.IsAny<CancellationToken>()), Times.Once);

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

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");

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

        // Assert - Processor catches ExternalDocumentNotSourcedException and publishes DocumentRequested + retry DocumentCreated
        bus.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.Athlete),
            It.IsAny<CancellationToken>()), Times.Once);

        bus.Verify(x => x.Publish(
            It.Is<DocumentCreated>(e => e.AttemptCount == command.AttemptCount + 1),
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

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");
        var dto = json.FromJson<EspnAthleteSeasonDto>();

        var athleteRef = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/{dto!.Id}";
        var athleteIdentity = generator.Generate(athleteRef);

        // Create only the Athlete (no FranchiseSeason)
        var athlete = Fixture.Build<FootballAthlete>()
            .WithAutoProperties()
            .With(x => x.Id, athleteIdentity.CanonicalId)
            .With(x => x.Seasons, new List<AthleteSeason>())
            .Create();

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
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.TeamSeason),
            It.IsAny<CancellationToken>()), Times.Once);

        bus.Verify(x => x.Publish(
            It.Is<DocumentCreated>(e => e.AttemptCount == command.AttemptCount + 1),
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

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");
        var dto = json.FromJson<EspnAthleteSeasonDto>();

        var franchiseSeasonIdentity = generator.Generate(dto!.Team.Ref!);
        var athleteRef = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/{dto.Id}";
        var athleteIdentity = generator.Generate(athleteRef);

        // Create Athlete and FranchiseSeason (but no Position)
        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .WithAutoProperties()
            .With(x => x.Id, franchiseSeasonIdentity.CanonicalId)
            .Create();

        var athlete = Fixture.Build<FootballAthlete>()
            .WithAutoProperties()
            .With(x => x.Id, athleteIdentity.CanonicalId)
            .With(x => x.Seasons, new List<AthleteSeason>())
            .Create();

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
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.AthletePosition),
            It.IsAny<CancellationToken>()), Times.Once);

        bus.Verify(x => x.Publish(
            It.Is<DocumentCreated>(e => e.AttemptCount == command.AttemptCount + 1),
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
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor>();

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
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor>();

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
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");
        var dto = json.FromJson<EspnAthleteSeasonDto>();

        // Setup all dependencies
        var dtoIdentity = generator.Generate(dto!.Ref);
        var franchiseSeasonIdentity = generator.Generate(dto.Team.Ref!);
        var positionIdentity = generator.Generate(dto.Position.Ref!);
        var athleteRef = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/{dto.Id}";
        var athleteIdentity = generator.Generate(athleteRef);

        var position = Fixture.Build<AthletePosition>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.ExternalIds, new List<AthletePositionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = positionIdentity.CleanUrl,
                    SourceUrlHash = positionIdentity.UrlHash,
                    Value = positionIdentity.UrlHash
                }
            })
            .Create();

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .WithAutoProperties()
            .With(x => x.Id, franchiseSeasonIdentity.CanonicalId)
            .Create();

        var athlete = Fixture.Build<FootballAthlete>()
            .WithAutoProperties()
            .With(x => x.Id, athleteIdentity.CanonicalId)
            .With(x => x.Seasons, new List<AthleteSeason>())
            .Create();

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
                e.ParentEntityId == dtoIdentity.CanonicalId &&
                e.Sport == Sport.FootballNcaa &&
                e.DocumentType == DocumentType.AthleteSeason),
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
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");
        var dto = json.FromJson<EspnAthleteSeasonDto>();

        // Setup all dependencies
        var dtoIdentity = generator.Generate(dto!.Ref);
        var franchiseSeasonIdentity = generator.Generate(dto.Team.Ref!);
        var positionIdentity = generator.Generate(dto.Position.Ref!);
        var athleteRef = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/{dto.Id}";
        var athleteIdentity = generator.Generate(athleteRef);

        var position = Fixture.Build<AthletePosition>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.ExternalIds, new List<AthletePositionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = positionIdentity.CleanUrl,
                    SourceUrlHash = positionIdentity.UrlHash,
                    Value = positionIdentity.UrlHash
                }
            })
            .Create();

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .WithAutoProperties()
            .With(x => x.Id, franchiseSeasonIdentity.CanonicalId)
            .Create();

        var athlete = Fixture.Build<FootballAthlete>()
            .WithAutoProperties()
            .With(x => x.Id, athleteIdentity.CanonicalId)
            .With(x => x.Seasons, new List<AthleteSeason>())
            .Create();

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
}

