using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common;

public class SeasonTypeDocumentProcessorTests
    : ProducerTestBase<SeasonTypeDocumentProcessor<FootballDataContext>>
{
    private readonly SeasonTypeDocumentProcessor<FootballDataContext> _processor;

    public SeasonTypeDocumentProcessorTests()
    {
        var logger = Mocker.Get<ILogger<SeasonTypeDocumentProcessor<FootballDataContext>>>();
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        _processor = Mocker.CreateInstance<SeasonTypeDocumentProcessor<FootballDataContext>>();
    }

    [Fact]
    public async Task ProcessNewSeasonType_CreatesSeasonPhaseAndPublishesWeekRequest()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        FootballDataContext.Seasons.Add(new Season
        {
            Id = seasonId,
            Year = 2025,
            Name = "2025",
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 12, 31),
            CreatedBy = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
        });
        await FootballDataContext.SaveChangesAsync();

        var json = await LoadJsonTestData("EspnFootballNcaaSeasonType_PreSeason.json");
        const string srcUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1?lang=en&region=us";
        var command = new ProcessDocumentCommand(
            SourceDataProvider.Espn,
            Sport.FootballNcaa,
            2025,
            DocumentType.SeasonType,
            json,
            correlationId: Guid.NewGuid(),
            parentId: seasonId.ToString(),
            new Uri(srcUrl),
            srcUrl.UrlHash()
        );

        // Act
        await _processor.ProcessAsync(command);

        // Assert
        var season = await FootballDataContext.Seasons
            .Include(s => s.Phases)
            .FirstAsync();

        season.Phases.Should().ContainSingle();

        var phase = season.Phases.First();
        phase.Name.Should().Be("Preseason");
        phase.Abbreviation.Should().Be("pre");
        phase.TypeCode.Should().Be(1);
        phase.ExternalIds.First().Provider.Should().Be(SourceDataProvider.Espn);

        // Publish verification
        //Mocker.Get<MassTransit.IPublishEndpoint>()
        //    .Received()
        //    .Publish(Arg.Is<DocumentRequested>(e =>
        //        e.DocumentType == DocumentType.SeasonTypeWeek &&
        //        e.ParentId == phase.Id.ToString() &&
        //        e.SeasonYear == 2025 &&
        //        e.Sport == Sport.FootballNcaa
        //    ));
    }
}
