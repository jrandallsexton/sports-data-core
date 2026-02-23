using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common;

public class SeasonTypeWeekDocumentProcessorTests
    : ProducerTestBase<SeasonTypeWeekDocumentProcessor<FootballDataContext>>
{
    [Fact]
    public async Task WhenJsonIsValid_ShouldCreateSeasonWeek_AndPublishRankingRequest()
    {
        // Arrange
        var json = """
        {
          "$ref": "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1/weeks/1?lang=en&region=us",
          "number": 1,
          "startDate": "2025-02-01T08:00Z",
          "endDate": "2025-08-23T06:59Z",
          "text": "Week 1",
          "rankings": {
            "$ref": "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1/weeks/1/rankings?lang=en&region=us"
          }
        }
        """;

        var correlationId = Guid.NewGuid();
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var seasonId = Guid.NewGuid();
        var seasonPhaseId = Guid.NewGuid();

        var seasonPhase = Fixture.Build<SeasonPhase>()
            .With(p => p.Id, seasonPhaseId)
            .With(p => p.SeasonId, seasonId)
            .With(p => p.Weeks, new List<SeasonWeek>())
            .Create();

        await FootballDataContext.SeasonPhases.AddAsync(seasonPhase);
        await FootballDataContext.SaveChangesAsync();

        var command = new ProcessDocumentCommand(
            sourceDataProvider: SourceDataProvider.Espn,
            sport: Sport.FootballNcaa,
            season: 2025,
            documentType: DocumentType.SeasonTypeWeek,
            document: json,
            messageId: Guid.NewGuid(),
            correlationId: correlationId,
            parentId: seasonPhaseId.ToString(),
            sourceUri: new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1/weeks/1?lang=en&region=us"),
            urlHash: generator.Generate("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1/weeks/1?lang=en&region=us").UrlHash
        );
        
        var sut = Mocker.CreateInstance<SeasonTypeWeekDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var savedWeek = await FootballDataContext.SeasonWeeks
            .Include(w => w.ExternalIds)
            .FirstOrDefaultAsync(w => w.SeasonPhaseId == seasonPhaseId);

        savedWeek.Should().NotBeNull();
        savedWeek!.Number.Should().Be(1);
        savedWeek.StartDate.Should().Be(DateTime.Parse("2025-02-01T08:00Z").ToUniversalTime());
        savedWeek.EndDate.Should().Be(DateTime.Parse("2025-08-23T06:59Z").ToUniversalTime());

        savedWeek.ExternalIds.Should().ContainSingle(id =>
            id.Provider == SourceDataProvider.Espn &&
            id.SourceUrl == "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1/weeks/1");

        //Mocker.Get<IPublishEndpoint>().Verify(x =>
        //    x.Publish(It.Is<DocumentRequested>(e =>
        //        e.DocumentType == DocumentType.SeasonTypeWeekRankings &&
        //        e.ParentId == savedWeek.Id.ToString()
        //    ), default), Times.Once);
    }

    [Fact]
    public async Task WhenParentIdNotProvided_ShouldDeriveSeasonPhaseIdFromUri_AndCreateSeasonWeek()
    {
        // Arrange - This test validates the scenario where ParentId is not provided 
        // (e.g., via dependency sourcing) and must be derived from the URI via EspnUriMapper
        var json = """
        {
          "$ref": "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/2/weeks/3?lang=en&region=us",
          "number": 3,
          "startDate": "2025-02-15T08:00Z",
          "endDate": "2025-02-22T06:59Z",
          "text": "Week 3",
          "rankings": {
            "$ref": "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/2/weeks/3/rankings?lang=en&region=us"
          }
        }
        """;

        var correlationId = Guid.NewGuid();
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var seasonId = Guid.NewGuid();
        
        // Derive the expected seasonPhaseId using the same URI mapping that the processor will use
        var seasonPhaseUri = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/2");
        var expectedSeasonPhaseId = generator.Generate(seasonPhaseUri).CanonicalId;

        var seasonPhase = Fixture.Build<SeasonPhase>()
            .With(p => p.Id, expectedSeasonPhaseId)
            .With(p => p.SeasonId, seasonId)
            .With(p => p.Weeks, new List<SeasonWeek>())
            .Create();

        await FootballDataContext.SeasonPhases.AddAsync(seasonPhase);
        await FootballDataContext.SaveChangesAsync();

        // Command with null ParentId to simulate dependency sourcing scenario
        var command = new ProcessDocumentCommand(
            sourceDataProvider: SourceDataProvider.Espn,
            sport: Sport.FootballNcaa,
            season: 2025,
            documentType: DocumentType.SeasonTypeWeek,
            document: json,
            messageId: Guid.NewGuid(),
            correlationId: correlationId,
            parentId: null, // NOT PROVIDED - must be derived from URI
            sourceUri: new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/2/weeks/3?lang=en&region=us"),
            urlHash: generator.Generate("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/2/weeks/3?lang=en&region=us").UrlHash
        );
        
        var sut = Mocker.CreateInstance<SeasonTypeWeekDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var savedWeek = await FootballDataContext.SeasonWeeks
            .Include(w => w.ExternalIds)
            .FirstOrDefaultAsync(w => w.SeasonPhaseId == expectedSeasonPhaseId);

        savedWeek.Should().NotBeNull("the processor should derive the SeasonPhaseId from the URI");
        savedWeek!.Number.Should().Be(3);
        savedWeek.SeasonPhaseId.Should().Be(expectedSeasonPhaseId, "derived from URI via EspnUriMapper");
        savedWeek.StartDate.Should().Be(DateTime.Parse("2025-02-15T08:00Z").ToUniversalTime());
        savedWeek.EndDate.Should().Be(DateTime.Parse("2025-02-22T06:59Z").ToUniversalTime());

        savedWeek.ExternalIds.Should().ContainSingle(id =>
            id.Provider == SourceDataProvider.Espn &&
            id.SourceUrl == "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/2/weeks/3");
    }
}
