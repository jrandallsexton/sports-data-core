using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

/// <summary>
/// Tests for AthleteSeasonStatisticsDocumentProcessor covering create and replace scenarios.
/// </summary>
[Collection("Sequential")]
public class AthleteSeasonStatisticsDocumentProcessorTests :
    ProducerTestBase<AthleteSeasonStatisticsDocumentProcessor<FootballDataContext>>
{
    /// <summary>
    /// Validates that when a valid AthleteSeason exists and a valid statistics document is provided,
    /// the processor creates a new AthleteSeasonStatistic with all categories and stats properly mapped.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_CreatesStatistics_WhenAthleteSeasonExists_AndValidDocument()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonStatisticsDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeasonStatistics.json");
        var dto = json.FromJson<EspnAthleteSeasonStatisticsDto>();

        // Create AthleteSeason
        var athleteSeason = Fixture.Build<FootballAthleteSeason>()
            .WithAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Statistics, new List<AthleteSeasonStatistic>())
            .Create();

        await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2023)
            .With(x => x.DocumentType, DocumentType.AthleteSeasonStatistics)
            .With(x => x.Document, json)
            .With(x => x.ParentId, athleteSeason.Id.ToString())
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var savedStatistic = await FootballDataContext.AthleteSeasonStatistics
            .Include(x => x.Categories)
                .ThenInclude(c => c.Stats)
            .Where(x => x.AthleteSeasonId == athleteSeason.Id)
            .FirstOrDefaultAsync();

        savedStatistic.Should().NotBeNull();
        savedStatistic!.AthleteSeasonId.Should().Be(athleteSeason.Id);
        savedStatistic.SplitId.Should().Be("0");
        savedStatistic.SplitName.Should().Be("Season");
        savedStatistic.SplitAbbreviation.Should().Be("Season");
        savedStatistic.SplitType.Should().Be("season");

        // Verify categories were created
        savedStatistic.Categories.Should().NotBeNullOrEmpty();
        savedStatistic.Categories.Should().HaveCount(5, "JSON has 5 stat categories: general, passing, rushing, receiving, scoring");

        // Verify category names
        var categoryNames = savedStatistic.Categories.Select(c => c.Name).ToList();
        categoryNames.Should().Contain("general");
        categoryNames.Should().Contain("passing");
        categoryNames.Should().Contain("rushing");
        categoryNames.Should().Contain("receiving");
        categoryNames.Should().Contain("scoring");

        // Verify stats were created for each category
        foreach (var category in savedStatistic.Categories)
        {
            category.Stats.Should().NotBeNullOrEmpty($"category '{category.Name}' should have stats");
        }

        // Spot-check specific stats from JSON
        var rushingCategory = savedStatistic.Categories.FirstOrDefault(c => c.Name == "rushing");
        rushingCategory.Should().NotBeNull();

        var rushingYardsStat = rushingCategory!.Stats.FirstOrDefault(s => s.Name == "rushingYards");
        rushingYardsStat.Should().NotBeNull();
        rushingYardsStat!.Value.Should().Be(121.0m);
        rushingYardsStat.DisplayValue.Should().Be("121");

        var rushingTdsStat = rushingCategory.Stats.FirstOrDefault(s => s.Name == "rushingTouchdowns");
        rushingTdsStat.Should().NotBeNull();
        rushingTdsStat!.Value.Should().Be(1.0m);

        // Verify total stats count is reasonable (JSON has many stats per category)
        var totalStats = savedStatistic.Categories.Sum(c => c.Stats.Count);
        totalStats.Should().BeGreaterThan(50, "JSON contains many stats across all categories");
    }

    /// <summary>
    /// Validates that when statistics already exist for an AthleteSeason,
    /// the processor removes the old statistics and replaces them with new data (wholesale replacement).
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ReplacesExistingStatistics_WhenStatisticsAlreadyExist()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteSeasonStatisticsDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeasonStatistics.json");
        var dto = json.FromJson<EspnAthleteSeasonStatisticsDto>();

        var dtoIdentity = generator.Generate(dto!.Ref);

        // Create AthleteSeason
        var athleteSeason = Fixture.Build<FootballAthleteSeason>()
            .WithAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Statistics, new List<AthleteSeasonStatistic>())
            .Create();

        // Create existing statistics that should be replaced
        var existingStatistic = new AthleteSeasonStatistic
        {
            Id = dtoIdentity.CanonicalId, // Same ID so it will be found and replaced
            AthleteSeasonId = athleteSeason.Id,
            SplitId = "old",
            SplitName = "Old Split",
            SplitAbbreviation = "OLD",
            SplitType = "old",
            CreatedUtc = DateTime.UtcNow.AddDays(-1),
            Categories = new List<AthleteSeasonStatisticCategory>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "oldCategory",
                    DisplayName = "Old Category",
                    ShortDisplayName = "OLD",
                    Abbreviation = "old",
                    CreatedUtc = DateTime.UtcNow.AddDays(-1),
                    Stats = new List<AthleteSeasonStatisticStat>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            Name = "oldStat",
                            DisplayName = "Old Stat",
                            ShortDisplayName = "OS",
                            Abbreviation = "OS",
                            Value = 999m,
                            DisplayValue = "999",
                            CreatedUtc = DateTime.UtcNow.AddDays(-1)
                        }
                    }
                }
            }
        };

        await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);
        await FootballDataContext.AthleteSeasonStatistics.AddAsync(existingStatistic);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2023)
            .With(x => x.DocumentType, DocumentType.AthleteSeasonStatistics)
            .With(x => x.Document, json)
            .With(x => x.ParentId, athleteSeason.Id.ToString())
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var statistics = await FootballDataContext.AthleteSeasonStatistics
            .Include(x => x.Categories)
                .ThenInclude(c => c.Stats)
            .Where(x => x.AthleteSeasonId == athleteSeason.Id)
            .ToListAsync();

        // Should have exactly 1 statistic (old one replaced, not duplicated)
        statistics.Should().HaveCount(1);

        var savedStatistic = statistics.First();

        // Verify it's the new data, not the old
        savedStatistic.SplitName.Should().Be("Season");
        savedStatistic.SplitName.Should().NotBe("Old Split");

        savedStatistic.Categories.Should().HaveCount(5);
        savedStatistic.Categories.Should().NotContain(c => c.Name == "oldCategory");
        savedStatistic.Categories.Should().Contain(c => c.Name == "rushing");
    }

    /// <summary>
    /// Validates that when the AthleteSeason does not exist,
    /// the processor logs an error and does not create statistics.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_DoesNothing_WhenAthleteSeasonNotFound()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var sut = Mocker.CreateInstance<AthleteSeasonStatisticsDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeasonStatistics.json");

        var nonExistentAthleteSeasonId = Guid.NewGuid();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2023)
            .With(x => x.DocumentType, DocumentType.AthleteSeasonStatistics)
            .With(x => x.Document, json)
            .With(x => x.ParentId, nonExistentAthleteSeasonId.ToString())
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert - Query only for statistics related to the non-existent AthleteSeason
        var statisticsForNonExistentAthlete = await FootballDataContext.AthleteSeasonStatistics
            .Where(x => x.AthleteSeasonId == nonExistentAthleteSeasonId)
            .ToListAsync();
        
        statisticsForNonExistentAthlete.Should().BeEmpty("no statistics should be created when AthleteSeason doesn't exist");
    }

    /// <summary>
    /// Validates that when the ParentId is not a valid GUID,
    /// the processor logs an error and does not process the document.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_DoesNothing_WhenParentIdInvalid()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var sut = Mocker.CreateInstance<AthleteSeasonStatisticsDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeasonStatistics.json");

        var initialCount = await FootballDataContext.AthleteSeasonStatistics.CountAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2023)
            .With(x => x.DocumentType, DocumentType.AthleteSeasonStatistics)
            .With(x => x.Document, json)
            .With(x => x.ParentId, "not-a-guid")
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert - Verify count hasn't changed
        var finalCount = await FootballDataContext.AthleteSeasonStatistics.CountAsync();
        finalCount.Should().Be(initialCount, "no new statistics should be created when ParentId is invalid");
    }

    /// <summary>
    /// Validates that when the document JSON is null or cannot be deserialized,
    /// the processor logs an error and does not create statistics.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_DoesNothing_WhenDocumentIsNull()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var sut = Mocker.CreateInstance<AthleteSeasonStatisticsDocumentProcessor<FootballDataContext>>();

        var athleteSeason = Fixture.Build<FootballAthleteSeason>()
            .WithAutoProperties()
            .With(x => x.Statistics, new List<AthleteSeasonStatistic>())
            .Create();

        await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2023)
            .With(x => x.DocumentType, DocumentType.AthleteSeasonStatistics)
            .With(x => x.Document, "null")
            .With(x => x.ParentId, athleteSeason.Id.ToString())
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert - Query only for statistics related to this specific AthleteSeason
        var statisticsForThisAthlete = await FootballDataContext.AthleteSeasonStatistics
            .Where(x => x.AthleteSeasonId == athleteSeason.Id)
            .ToListAsync();
        
        statisticsForThisAthlete.Should().BeEmpty("no statistics should be created when document is null");
    }

    /// <summary>
    /// Validates that when the DTO is missing the $ref property,
    /// the processor logs an error and does not create statistics.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_DoesNothing_WhenRefIsNull()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var sut = Mocker.CreateInstance<AthleteSeasonStatisticsDocumentProcessor<FootballDataContext>>();

        var athleteSeason = Fixture.Build<FootballAthleteSeason>()
            .WithAutoProperties()
            .With(x => x.Statistics, new List<AthleteSeasonStatistic>())
            .Create();

        await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);
        await FootballDataContext.SaveChangesAsync();

        // Create JSON without $ref (will fail in processor before creating anything)
        var invalidJson = "{\"season\":{},\"athlete\":{},\"splits\":{\"id\":\"0\",\"name\":\"Season\",\"abbreviation\":\"Season\",\"type\":\"season\",\"categories\":[]},\"seasonType\":{}}";

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2023)
            .With(x => x.DocumentType, DocumentType.AthleteSeasonStatistics)
            .With(x => x.Document, invalidJson)
            .With(x => x.ParentId, athleteSeason.Id.ToString())
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert - Query only for statistics related to this specific AthleteSeason
        var statisticsForThisAthlete = await FootballDataContext.AthleteSeasonStatistics
            .Where(x => x.AthleteSeasonId == athleteSeason.Id)
            .ToListAsync();
        
        statisticsForThisAthlete.Should().BeEmpty("no statistics should be created when $ref is null");
    }

    /// <summary>
    /// Validates that all stat values including perGameValue and perGameDisplayValue
    /// are correctly mapped from the DTO.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_MapsPerGameValues_Correctly()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var sut = Mocker.CreateInstance<AthleteSeasonStatisticsDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeasonStatistics.json");

        var athleteSeason = Fixture.Build<FootballAthleteSeason>()
            .WithAutoProperties()
            .With(x => x.Statistics, new List<AthleteSeasonStatistic>())
            .Create();

        await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2023)
            .With(x => x.DocumentType, DocumentType.AthleteSeasonStatistics)
            .With(x => x.Document, json)
            .With(x => x.ParentId, athleteSeason.Id.ToString())
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var savedStatistic = await FootballDataContext.AthleteSeasonStatistics
            .Include(x => x.Categories)
                .ThenInclude(c => c.Stats)
            .Where(x => x.AthleteSeasonId == athleteSeason.Id)
            .FirstOrDefaultAsync();

        savedStatistic.Should().NotBeNull();

        // Find a stat that has perGameValue in the JSON
        var rushingCategory = savedStatistic!.Categories.FirstOrDefault(c => c.Name == "rushing");
        rushingCategory.Should().NotBeNull();
        
        var rushingYardsStat = rushingCategory!.Stats.FirstOrDefault(s => s.Name == "rushingYards");

        rushingYardsStat.Should().NotBeNull();
        rushingYardsStat!.PerGameValue.Should().Be(17.0m, "JSON has perGameValue of 17");
        rushingYardsStat.PerGameDisplayValue.Should().Be("17", "JSON has perGameDisplayValue of '17'");
    }
}
