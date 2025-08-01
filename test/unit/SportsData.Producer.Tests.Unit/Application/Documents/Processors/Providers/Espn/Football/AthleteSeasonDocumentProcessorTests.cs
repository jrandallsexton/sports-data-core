using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

public class AthleteSeasonDocumentProcessorTests :
    ProducerTestBase<AthleteSeasonDocumentProcessor>
{
    private const string SourceUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/4426333";
    private readonly string _urlHash = SourceUrl.UrlHash();

    [Fact]
    public async Task WhenAthleteSeasonIsValid_ShouldCreateAthleteSeason()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var bus = Mocker.GetMock<IPublishEndpoint>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor>();
        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");

        var position = Fixture.Build<AthletePosition>()
            .WithAutoProperties()
            .With(x => x.Abbreviation, "QB")
            .Create();

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .WithAutoProperties()
            .With(x => x.SeasonYear, 2024)
            .Create();

        var athlete = Fixture.Build<FootballAthlete>()
            .WithAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
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
            .With(x => x.ParentId, athlete.Id.ToString())
            .With(x => x.UrlHash, _urlHash)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var entity = await FootballDataContext.AthleteSeasons.FirstOrDefaultAsync();
        entity.Should().NotBeNull();
        entity!.AthleteId.Should().Be(athlete.Id);
        entity.PositionId.Should().Be(position.Id);
        entity.FranchiseSeasonId.Should().Be(franchiseSeason.Id);

        // Replace when event exists:
        bus.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
