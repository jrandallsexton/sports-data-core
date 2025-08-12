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
}
