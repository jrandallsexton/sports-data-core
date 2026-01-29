using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

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
/// Tests for EventCompetitionAthleteStatisticsDocumentProcessor covering create and replace scenarios.
/// </summary>
[Collection("Sequential")]
public class EventCompetitionAthleteStatisticsDocumentProcessorTests
    : ProducerTestBase<EventCompetitionAthleteStatisticsDocumentProcessor<FootballDataContext>>
{
    /// <summary>
    /// Validates that when a valid Athlete, AthleteSeason (linked to FranchiseSeason with correct year), and Competition exist,
    /// the processor creates a new AthleteCompetitionStatistic with all categories and stats properly mapped.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_CreatesStatistics_WhenAthleteSeasonAndCompetitionExist_AndValidDocument()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<EventCompetitionAthleteStatisticsDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionAthleteStatistics.json");
        var dto = json.FromJson<EspnEventCompetitionAthleteStatisticsDto>();

        // Generate identities from the DTO's refs
        var athleteSeasonIdentity = generator.Generate(dto!.Athlete!.Ref!);
        var competitionIdentity = generator.Generate(dto.Competition!.Ref!);

        // For ESPN, canonical IDs are 1:1 with refs - create entities with canonical IDs
        var athleteSeason = Fixture.Build<FootballAthleteSeason>()
            .WithAutoProperties()
            .With(x => x.Id, athleteSeasonIdentity.CanonicalId)
            .Create();

        await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);

        var competition = Fixture.Build<Competition>()
            .WithAutoProperties()
            .With(x => x.Id, competitionIdentity.CanonicalId)
            .Create();

        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2025)
            .With(x => x.DocumentType, DocumentType.EventCompetitionAthleteStatistics)
            .With(x => x.Document, json)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var savedStatistic = await FootballDataContext.AthleteCompetitionStatistics
            .Include(x => x.Categories)
                .ThenInclude(c => c.Stats)
            .Where(x => x.AthleteSeasonId == athleteSeason.Id && x.CompetitionId == competition.Id)
            .FirstOrDefaultAsync();

        savedStatistic.Should().NotBeNull();
        savedStatistic!.AthleteSeasonId.Should().Be(athleteSeason.Id);
        savedStatistic.CompetitionId.Should().Be(competition.Id);

        // Verify categories were created
        savedStatistic.Categories.Should().NotBeNullOrEmpty();
        
        // Verify category names exist from JSON
        var categoryNames = savedStatistic.Categories.Select(c => c.Name).ToList();
        categoryNames.Should().Contain("general");

        // Verify stats were created for each category
        foreach (var category in savedStatistic.Categories)
        {
            category.Stats.Should().NotBeNullOrEmpty($"category '{category.Name}' should have stats");
        }

        // Verify total stats count is reasonable
        var totalStats = savedStatistic.Categories.Sum(c => c.Stats.Count);
        totalStats.Should().BeGreaterThan(0, "JSON contains stats across all categories");
    }

    /// <summary>
    /// Validates that when statistics already exist for an Athlete+Competition,
    /// the processor removes the old statistics and replaces them with new data (wholesale replacement).
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ReplacesExistingStatistics_WhenStatisticsAlreadyExist()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<EventCompetitionAthleteStatisticsDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionAthleteStatistics.json");
        var dto = json.FromJson<EspnEventCompetitionAthleteStatisticsDto>();

        // Resolve identities from DTO refs
        var dtoIdentity = generator.Generate(dto!.Ref!);
        var athleteSeasonIdentity = generator.Generate(dto.Athlete!.Ref!);
        var competitionIdentity = generator.Generate(dto.Competition!.Ref!);

        // For ESPN, canonical IDs are 1:1 with refs - create entities with canonical IDs
        var athleteSeason = Fixture.Build<FootballAthleteSeason>()
            .WithAutoProperties()
            .With(x => x.Id, athleteSeasonIdentity.CanonicalId)
            .Create();

        await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);

        var competition = Fixture.Build<Competition>()
            .WithAutoProperties()
            .With(x => x.Id, competitionIdentity.CanonicalId)
            .Create();

        await FootballDataContext.Competitions.AddAsync(competition);

        // Create existing statistics that should be replaced
        var existingStatistic = new AthleteCompetitionStatistic
        {
            Id = dtoIdentity.CanonicalId, // Same ID so it will be found and replaced
            AthleteSeasonId = athleteSeason.Id,
            CompetitionId = competition.Id,
            CreatedUtc = DateTime.UtcNow.AddDays(-1),
            Categories = new List<AthleteCompetitionStatisticCategory>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "oldCategory",
                    DisplayName = "Old Category",
                    ShortDisplayName = "OLD",
                    Abbreviation = "old",
                    CreatedUtc = DateTime.UtcNow.AddDays(-1),
                    Stats = new List<AthleteCompetitionStatisticStat>
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

        await FootballDataContext.AthleteCompetitionStatistics.AddAsync(existingStatistic);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2025)
            .With(x => x.DocumentType, DocumentType.EventCompetitionAthleteStatistics)
            .With(x => x.Document, json)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert - old statistics should be replaced with new
        var statistics = await FootballDataContext.AthleteCompetitionStatistics
            .Include(x => x.Categories)
                .ThenInclude(c => c.Stats)
            .Where(x => x.AthleteSeasonId == athleteSeason.Id && x.CompetitionId == competition.Id)
            .ToListAsync();

        // Should have exactly 1 statistic (old one replaced, not duplicated)
        statistics.Should().HaveCount(1);

        var savedStatistic = statistics.First();

        // Old category should be gone
        savedStatistic.Categories.Should().NotContain(c => c.Name == "oldCategory");
        
        // New categories from JSON should exist
        var categoryNames = savedStatistic.Categories.Select(c => c.Name).ToList();
        categoryNames.Should().Contain("general", "should have new data from JSON");

        // Old stat should be gone
        foreach (var category in savedStatistic.Categories)
        {
            category.Stats.Should().NotContain(s => s.Name == "oldStat");
        }
        
        // Should have actual stats from JSON
        var totalStats = savedStatistic.Categories.Sum(c => c.Stats.Count);
        totalStats.Should().BeGreaterThan(0, "should have stats from JSON");
    }
}

