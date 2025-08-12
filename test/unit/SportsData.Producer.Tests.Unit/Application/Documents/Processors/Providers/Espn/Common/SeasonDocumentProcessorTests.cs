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

        Assert.NotNull(season);
        Assert.Equal(2025, season.Year);
        Assert.Empty(season.Phases);
        Assert.NotEmpty(season.ExternalIds);
        Assert.All(season.ExternalIds, id => Assert.Equal(SourceDataProvider.Espn, id.Provider));
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

        Assert.NotNull(season);
        Assert.Equal(2024, season.Year);
        Assert.Empty(season.Phases);
        Assert.NotEmpty(season.ExternalIds);
        Assert.All(season.ExternalIds, id => Assert.Equal(SourceDataProvider.Espn, id.Provider));
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

        Assert.NotNull(season);
        var initialPhaseCount = season.Phases.Count;
        var initialExternalIdCount = season.ExternalIds.Count;

        // Act - re-ingest same document
        await _processor.ProcessAsync(command);

        // Assert
        var updatedSeason = await FootballDataContext.Seasons
            .Include(s => s.Phases)
            .Include(s => s.ExternalIds)
            .FirstOrDefaultAsync();

        Assert.NotNull(updatedSeason);
        Assert.Equal(season.Id, updatedSeason.Id);
        Assert.Equal(initialPhaseCount, updatedSeason.Phases.Count);
        Assert.Equal(initialExternalIdCount, updatedSeason.ExternalIds.Count);

        // Updated check for ActivePhaseId
        if (updatedSeason.ActivePhaseId != null)
        {
            Assert.Contains(updatedSeason.Phases, p => p.Id == updatedSeason.ActivePhaseId);
        }

    }
}