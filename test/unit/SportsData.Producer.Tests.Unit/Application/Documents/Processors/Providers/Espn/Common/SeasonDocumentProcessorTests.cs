using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common;

public class SeasonDocumentProcessorTests
    : ProducerTestBase<SeasonDocumentProcessor<FootballDataContext>>
{
    private readonly SeasonDocumentProcessor<FootballDataContext> _processor;

    public SeasonDocumentProcessorTests()
    {
        var logger = Mocker.Get<ILogger<SeasonDocumentProcessor<FootballDataContext>>>();

        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        _processor = Mocker.CreateInstance<SeasonDocumentProcessor<FootballDataContext>>();
    }

    [Fact]
    public async Task ProcessNewSeason_CreatesSeasonEntity_2025()
    {
        // Arrange
        const string srcUrl =
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025?lang=en&region=us";
        var json = await LoadJsonTestData("EspnFootballNcaaSeason2025.json");
        var command = new ProcessDocumentCommand(
            SourceDataProvider.Espn,
            Sport.FootballNcaa,
            2025,
            DocumentType.Season,
            json,
            Guid.NewGuid(),
            parentId: null,
            new Uri(srcUrl),
            srcUrl.UrlHash()
        );

        // Act
        await _processor.ProcessAsync(command);

        // Assert
        var season = await FootballDataContext.Seasons
            .Include(s => s.Phases)
            .Include(s => s.ExternalIds)
            .FirstOrDefaultAsync();

        season.Should().NotBeNull();
        season!.Year.Should().Be(2025);
        season.Phases.Should().NotBeEmpty();
        season.ExternalIds.Should().NotBeEmpty();
        season.ExternalIds.Should().AllSatisfy(id => id.Provider.Should().Be(SourceDataProvider.Espn));
    }

    [Fact]
    public async Task ProcessNewSeason_CreatesSeasonEntity_2024()
    {
        // Arrange
        const string srcUrl =
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024?lang=en&region=us";
        var json = await LoadJsonTestData("EspnFootballNcaaSeason2024.json");
        var command = new ProcessDocumentCommand(
            SourceDataProvider.Espn,
            Sport.FootballNcaa,
            2024,
            DocumentType.Season,
            json,
            Guid.NewGuid(),
            parentId: null,
            new Uri(srcUrl),
            srcUrl.UrlHash()
        );

        // Act
        await _processor.ProcessAsync(command);

        // Assert
        var season = await FootballDataContext.Seasons
            .Include(s => s.Phases)
            .Include(s => s.ExternalIds)
            .FirstOrDefaultAsync();

        season.Should().NotBeNull();
        season!.Year.Should().Be(2024);
        season.Phases.Should().NotBeEmpty();
        season.ExternalIds.Should().NotBeEmpty();
        season.ExternalIds.Should().AllSatisfy(id => id.Provider.Should().Be(SourceDataProvider.Espn));
    }

    [Fact]
    public async Task ProcessExistingSeason_UpdatesPhasesAndExternalIds()
    {
        // Arrange - first ingestion
        const string srcUrl =
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024?lang=en&region=us";
        var json2025 = await LoadJsonTestData("EspnFootballNcaaSeason2025.json");
        var command = new ProcessDocumentCommand(
            SourceDataProvider.Espn,
            Sport.FootballNcaa,
            2025,
            DocumentType.Season,
            json2025,
            Guid.NewGuid(),
            parentId: null,
            new Uri(srcUrl),
            srcUrl.UrlHash()
        );

        await _processor.ProcessAsync(command);

        var season = await FootballDataContext.Seasons
            .Include(s => s.Phases)
            .Include(s => s.ExternalIds)
            .FirstOrDefaultAsync();

        season.Should().NotBeNull();
        var initialPhaseCount = season!.Phases.Count;
        var initialExternalIdCount = season.ExternalIds.Count;

        // Act - re-ingest same document
        await _processor.ProcessAsync(command);

        // Assert
        var updatedSeason = await FootballDataContext.Seasons
            .Include(s => s.Phases)
            .Include(s => s.ExternalIds)
            .FirstOrDefaultAsync();

        updatedSeason.Should().NotBeNull();
        updatedSeason!.Id.Should().Be(season.Id);
        updatedSeason.Phases.Should().HaveCount(initialPhaseCount);
        updatedSeason.ExternalIds.Should().HaveCount(initialExternalIdCount);

        // Updated check for ActivePhaseId
        if (updatedSeason.ActivePhaseId != null)
        {
            updatedSeason.Phases.Should().Contain(p => p.Id == updatedSeason.ActivePhaseId);
        }

    }
}