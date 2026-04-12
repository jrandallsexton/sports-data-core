#nullable enable

using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common.Draft;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

[Collection("Sequential")]
public class DraftDocumentProcessorTests :
    ProducerTestBase<DraftDocumentProcessor<FootballDataContext>>
{
    [Fact]
    public async Task ProcessAsync_CreatesDraftEntity_WhenNewDraft()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<DraftDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNfl/EspnFootballNflDraft.json");
        var dto = json.FromJson<EspnDraftDto>();
        var draftIdentity = generator.Generate(dto!.Ref);

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNfl)
            .With(x => x.SeasonYear, 2024)
            .With(x => x.DocumentType, DocumentType.Draft)
            .With(x => x.Document, json)
            .With(x => x.UrlHash, draftIdentity.UrlHash)
            .Without(x => x.ParentId)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var entity = await FootballDataContext.Drafts.FirstOrDefaultAsync();

        entity.Should().NotBeNull();
        entity!.Id.Should().Be(draftIdentity.CanonicalId);
        entity.Year.Should().Be(2024);
        entity.NumberOfRounds.Should().Be(7);
        entity.DisplayName.Should().Be("2024 National Football League Draft");
    }

    [Fact]
    public async Task ProcessAsync_UpdatesExistingDraft_WhenDraftAlreadyExists()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<DraftDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNfl/EspnFootballNflDraft.json");
        var dto = json.FromJson<EspnDraftDto>();
        var draftIdentity = generator.Generate(dto!.Ref);

        // Seed an existing Draft entity
        var existingDraft = new Draft
        {
            Id = draftIdentity.CanonicalId,
            Year = 2024,
            NumberOfRounds = 5, // intentionally wrong to verify update
            DisplayName = "Old Draft Name",
            ShortDisplayName = "Old",
            CreatedBy = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow
        };

        await FootballDataContext.Drafts.AddAsync(existingDraft);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNfl)
            .With(x => x.SeasonYear, 2024)
            .With(x => x.DocumentType, DocumentType.Draft)
            .With(x => x.Document, json)
            .With(x => x.UrlHash, draftIdentity.UrlHash)
            .Without(x => x.ParentId)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var drafts = await FootballDataContext.Drafts.ToListAsync();
        drafts.Should().HaveCount(1, "existing draft should be updated, not duplicated");

        var entity = drafts.Single();
        entity.NumberOfRounds.Should().Be(7);
        entity.DisplayName.Should().Be("2024 National Football League Draft");
    }

    [Fact]
    public async Task ProcessAsync_PublishesChildDocumentRequest_ForRounds()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<DraftDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNfl/EspnFootballNflDraft.json");
        var dto = json.FromJson<EspnDraftDto>();
        var draftIdentity = generator.Generate(dto!.Ref);

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNfl)
            .With(x => x.SeasonYear, 2024)
            .With(x => x.DocumentType, DocumentType.Draft)
            .With(x => x.Document, json)
            .With(x => x.UrlHash, draftIdentity.UrlHash)
            .Without(x => x.ParentId)
            .With(x => x.IncludeLinkedDocumentTypes, (IReadOnlyCollection<DocumentType>?)null)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert - verify DocumentRequested was published for DraftRounds
        bus.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.DraftRounds),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
