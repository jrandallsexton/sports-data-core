using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common.Draft;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

[Collection("Sequential")]
public class DraftRoundsDocumentProcessorTests :
    ProducerTestBase<DraftRoundsDocumentProcessor<FootballDataContext>>
{
    [Fact]
    public async Task ProcessAsync_CreatesDraftRoundsAndPicks_WhenDraftExists()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<DraftRoundsDocumentProcessor<FootballDataContext>>();

        // Load the draft root JSON to generate the deterministic draft ID
        var draftJson = await LoadJsonTestData("EspnFootballNflDraft.json");
        var draftDto = draftJson.FromJson<EspnDraftDto>();
        var draftIdentity = generator.Generate(draftDto!.Ref);

        // Seed a Draft entity (required parent)
        var draft = new Draft
        {
            Id = draftIdentity.CanonicalId,
            Year = 2024,
            NumberOfRounds = 7,
            DisplayName = "2024 National Football League Draft",
            ShortDisplayName = "2024 NFL Draft",
            CreatedBy = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow
        };

        await FootballDataContext.Drafts.AddAsync(draft);
        await FootballDataContext.SaveChangesAsync();

        // Load the rounds JSON
        var roundsJson = await LoadJsonTestData("EspnFootballNflDraftRounds.json");

        // The rounds JSON is a collection (no $ref at root), so generate UrlHash from the known rounds URI
        var roundsUri = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2024/draft/rounds");
        var roundsUrlHash = generator.Generate(roundsUri).UrlHash;

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNfl)
            .With(x => x.SeasonYear, 2024)
            .With(x => x.DocumentType, DocumentType.DraftRounds)
            .With(x => x.Document, roundsJson)
            .With(x => x.UrlHash, roundsUrlHash)
            .With(x => x.ParentId, draft.Id.ToString())
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var rounds = await FootballDataContext.DraftRounds
            .Include(r => r.Picks)
            .Where(r => r.DraftId == draft.Id)
            .ToListAsync();

        rounds.Should().HaveCount(7, "the 2024 NFL draft has 7 rounds");

        var totalPicks = rounds.Sum(r => r.Picks.Count);
        totalPicks.Should().Be(257, "the 2024 NFL draft has 257 total picks");

        // Verify first overall pick
        var firstRound = rounds.Single(r => r.Number == 1);
        var firstPick = firstRound.Picks.Single(p => p.Overall == 1);
        firstPick.Overall.Should().Be(1);
        firstPick.Pick.Should().Be(1);
    }

    [Fact]
    public async Task ProcessAsync_ThrowsInvalidOperationException_WhenDraftMissing()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<DraftRoundsDocumentProcessor<FootballDataContext>>();

        var roundsJson = await LoadJsonTestData("EspnFootballNflDraftRounds.json");

        var roundsUri = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2024/draft/rounds");
        var roundsUrlHash = generator.Generate(roundsUri).UrlHash;

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNfl)
            .With(x => x.SeasonYear, 2024)
            .With(x => x.DocumentType, DocumentType.DraftRounds)
            .With(x => x.Document, roundsJson)
            .With(x => x.UrlHash, roundsUrlHash)
            .Without(x => x.ParentId)
            .Create();

        // Act & Assert — Draft entity not seeded, processor throws InvalidOperationException
        var act = () => sut.ProcessAsync(command);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Draft entity not found*");
    }
}
