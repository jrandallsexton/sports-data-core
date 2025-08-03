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

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

public class EventCompetitionCompetitorScoreDocumentProcessorTests : ProducerTestBase<FootballDataContext>
{
    private const string ScoreUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/competitors/1/score";

    private ProcessDocumentCommand CreateCommand(string jsonFile, string? parentId = null)
    {
        var generator = new ExternalRefIdentityGenerator();
        return Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, jsonFile)
            .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitorScore)
            .With(x => x.Season, 2024)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, parentId ?? Guid.NewGuid().ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .With(x => x.UrlHash, generator.Generate(ScoreUrl).UrlHash)
            .Create();
    }

    [Fact]
    public async Task WhenEntityDoesNotExist_ShouldCreateScoreWithCorrectData()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var competitorId = Guid.NewGuid();
        var competitor = Fixture.Build<CompetitionCompetitor>()
            .With(x => x.Id, competitorId)
            .With(x => x.CreatedBy, Guid.NewGuid())
            .Create();
        await FootballDataContext.CompetitionCompetitors.AddAsync(competitor);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<EventCompetitionCompetitorScoreDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorScore.json");
        var command = CreateCommand(json, competitorId.ToString());

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var entity = await FootballDataContext.CompetitionCompetitorScores
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.CompetitionCompetitorId == competitorId);

        entity.Should().NotBeNull();
        entity!.ExternalIds.Should().NotBeEmpty();
        entity.CompetitionCompetitorId.Should().Be(competitorId);
        entity.DisplayValue.Should().NotBeNullOrEmpty();
        entity.SourceId.Should().NotBeNullOrEmpty();
        entity.SourceDescription.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task WhenParentIdIsInvalid_ShouldThrow()
    {
        // Arrange
        var sut = Mocker.CreateInstance<EventCompetitionCompetitorScoreDocumentProcessor<FootballDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorScore.json");

        var command = CreateCommand(json, "not-a-guid");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessAsync(command));
    }

    [Fact]
    public async Task WhenParentIdIsMissing_ShouldThrow()
    {
        // Arrange
        var sut = Mocker.CreateInstance<EventCompetitionCompetitorScoreDocumentProcessor<FootballDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorScore.json");

        var command = CreateCommand(json, null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessAsync(command));
    }

    [Fact]
    public async Task WhenCompetitionCompetitorDoesNotExist_ShouldThrow()
    {
        // Arrange
        var sut = Mocker.CreateInstance<EventCompetitionCompetitorScoreDocumentProcessor<FootballDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorScore.json");

        var command = CreateCommand(json, Guid.NewGuid().ToString());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessAsync(command));
    }
}
