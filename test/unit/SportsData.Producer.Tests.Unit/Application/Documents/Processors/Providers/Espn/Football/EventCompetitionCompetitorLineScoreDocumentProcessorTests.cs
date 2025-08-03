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

public class EventCompetitionCompetitorLineScoreDocumentProcessorTests : ProducerTestBase<FootballDataContext>
{
    private const string TestUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/competitors/1/linescores/1";

    private ProcessDocumentCommand CreateCommand(string jsonFile, string? parentId = null)
    {
        var generator = new ExternalRefIdentityGenerator();
        return Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, jsonFile)
            .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitorLineScore)
            .With(x => x.Season, 2024)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, parentId ?? Guid.NewGuid().ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .With(x => x.UrlHash, generator.Generate(TestUrl).UrlHash)
            .Create();
    }

    [Fact]
    public async Task WhenValid_ShouldCreateLineScore()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var competitorId = Guid.NewGuid();
        var competitor = Fixture.Build<CompetitionCompetitor>()
            .With(x => x.Id, competitorId)
            .With(x => x.CreatedBy, Guid.NewGuid())
            .With(x => x.LineScores, new List<CompetitionCompetitorLineScore>())
            .Create();
        await FootballDataContext.CompetitionCompetitors.AddAsync(competitor);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<EventCompetitionCompetitorLineScoreDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorLineScore.json");
        var command = CreateCommand(json, competitorId.ToString());

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var lineScore = await FootballDataContext.CompetitionCompetitorLineScores
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.CompetitionCompetitorId == competitorId);

        lineScore.Should().NotBeNull();
        lineScore!.ExternalIds.Should().NotBeEmpty();
        lineScore.Period.Should().Be(1);
        lineScore.Value.Should().Be(0);
        lineScore.DisplayValue.Should().Be("0");
        lineScore.SourceId.Should().Be("1");
        lineScore.SourceDescription.Should().Be("Basic/Manual");
        lineScore.SourceState.Should().BeNull(); // since `state` is missing in the JSON
    }

    [Fact]
    public async Task WhenParentIdInvalid_ShouldThrow()
    {
        var sut = Mocker.CreateInstance<EventCompetitionCompetitorLineScoreDocumentProcessor<FootballDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorLineScore.json");
        var command = CreateCommand(json, "not-a-guid");

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessAsync(command));
    }

    [Fact]
    public async Task WhenParentMissing_ShouldThrow()
    {
        var sut = Mocker.CreateInstance<EventCompetitionCompetitorLineScoreDocumentProcessor<FootballDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorLineScore.json");
        var command = CreateCommand(json, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessAsync(command));
    }
}
