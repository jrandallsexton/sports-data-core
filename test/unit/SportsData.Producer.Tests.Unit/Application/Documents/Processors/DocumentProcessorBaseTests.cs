using FluentAssertions;

using Microsoft.Extensions.Logging;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors;

/// <summary>
/// Unit tests for DocumentProcessorBase to verify critical dependency tracking logic.
/// Uses a minimal test processor implementation to test the abstract base class.
/// </summary>
public class DocumentProcessorBaseTests : ProducerTestBase<FootballDataContext>
{
    [Fact]
    public async Task PublishDependencyRequest_Should_Publish_On_First_Request()
    {
        // Arrange
        var busMock = Mocker.GetMock<IEventBus>();
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();

        var command = new ProcessDocumentCommand(
            sourceDataProvider: SourceDataProvider.Espn,
            sport: Sport.FootballNcaa,
            season: 2024,
            documentType: DocumentType.TeamSeason,
            document: "{}",
            messageId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(),
            parentId: null,
            sourceUri: new Uri("http://test.com"),
            urlHash: "test123",
            attemptCount: 0);

        var hasRef = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/2640") };

        // Act
        await processor.PublishDependencyRequestPublic(command, hasRef, Guid.NewGuid(), DocumentType.Franchise);

        // Assert
        busMock.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.Franchise),
            It.IsAny<CancellationToken>()), Times.Once);

        command.RequestedDependencies.Should().ContainSingle();
        command.RequestedDependencies.Should().Contain(d => d.Type == DocumentType.Franchise);
    }

    [Fact]
    public async Task PublishDependencyRequest_Should_Skip_Duplicate_On_Retry()
    {
        // Arrange
        var busMock = Mocker.GetMock<IEventBus>();
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();

        var franchiseRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/2640");
        var identity = generator.Generate(franchiseRef);

        var command = new ProcessDocumentCommand(
            sourceDataProvider: SourceDataProvider.Espn,
            sport: Sport.FootballNcaa,
            season: 2024,
            documentType: DocumentType.TeamSeason,
            document: "{}",
            messageId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(),
            parentId: null,
            sourceUri: new Uri("http://test.com"),
            urlHash: "test123",
            attemptCount: 1);

        // Simulate that this dependency was already requested
        command.RequestedDependencies.Add((DocumentType.Franchise, identity.UrlHash));

        var hasRef = new EspnLinkDto { Ref = franchiseRef };

        // Act
        await processor.PublishDependencyRequestPublic(command, hasRef, Guid.NewGuid(), DocumentType.Franchise);

        // Assert
        busMock.Verify(x => x.Publish(
            It.IsAny<DocumentRequested>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishDependencyRequest_Should_Publish_New_Dependency_On_Retry()
    {
        // Arrange
        var busMock = Mocker.GetMock<IEventBus>();
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();

        var franchiseARef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/2640");
        var franchiseBRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/333");
        var identityA = generator.Generate(franchiseARef);

        var command = new ProcessDocumentCommand(
            sourceDataProvider: SourceDataProvider.Espn,
            sport: Sport.FootballNcaa,
            season: 2024,
            documentType: DocumentType.EventCompetition,
            document: "{}",
            messageId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(),
            parentId: null,
            sourceUri: new Uri("http://test.com"),
            urlHash: "test123",
            attemptCount: 1);

        // Simulate that Franchise A was already requested
        command.RequestedDependencies.Add((DocumentType.Franchise, identityA.UrlHash));

        var hasRefB = new EspnLinkDto { Ref = franchiseBRef };

        // Act - Request Franchise B (different franchise)
        await processor.PublishDependencyRequestPublic(command, hasRefB, Guid.NewGuid(), DocumentType.Franchise);

        // Assert - Should publish for Franchise B even though we're on retry attempt
        busMock.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.Franchise),
            It.IsAny<CancellationToken>()), Times.Once);

        command.RequestedDependencies.Should().HaveCount(2);
    }

    [Fact]
    public async Task PublishDependencyRequest_Should_Track_Multiple_Dependencies_Of_Same_Type()
    {
        // Arrange
        var busMock = Mocker.GetMock<IEventBus>();
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();

        var command = new ProcessDocumentCommand(
            sourceDataProvider: SourceDataProvider.Espn,
            sport: Sport.FootballNcaa,
            season: 2024,
            documentType: DocumentType.EventCompetition,
            document: "{}",
            messageId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(),
            parentId: null,
            sourceUri: new Uri("http://test.com"),
            urlHash: "test123",
            attemptCount: 0);

        var franchiseARef = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/2640") };
        var franchiseBRef = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/333") };

        // Act - Request two different franchises (e.g., home and away teams)
        await processor.PublishDependencyRequestPublic(command, franchiseARef, Guid.NewGuid(), DocumentType.Franchise);
        await processor.PublishDependencyRequestPublic(command, franchiseBRef, Guid.NewGuid(), DocumentType.Franchise);

        // Assert - Both should be published and tracked
        busMock.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.Franchise),
            It.IsAny<CancellationToken>()), Times.Exactly(2));

        command.RequestedDependencies.Should().HaveCount(2);
        command.RequestedDependencies.Should().OnlyContain(d => d.Type == DocumentType.Franchise);
    }

    [Fact]
    public async Task PublishDependencyRequest_Should_Skip_When_Ref_Is_Null()
    {
        // Arrange
        var busMock = Mocker.GetMock<IEventBus>();
        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();

        var command = new ProcessDocumentCommand(
            sourceDataProvider: SourceDataProvider.Espn,
            sport: Sport.FootballNcaa,
            season: 2024,
            documentType: DocumentType.TeamSeason,
            document: "{}",
            messageId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(),
            parentId: null,
            sourceUri: new Uri("http://test.com"),
            urlHash: "test123",
            attemptCount: 0);

        var hasRef = new EspnLinkDto { Ref = null };

        // Act
        await processor.PublishDependencyRequestPublic(command, hasRef, Guid.NewGuid(), DocumentType.Franchise);

        // Assert
        busMock.Verify(x => x.Publish(
            It.IsAny<DocumentRequested>(),
            It.IsAny<CancellationToken>()), Times.Never);

        command.RequestedDependencies.Should().BeEmpty();
    }
}

/// <summary>
/// Minimal test implementation of DocumentProcessorBase for unit testing.
/// Exposes protected methods publicly to verify base class behavior.
/// </summary>
public class TestDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    public TestDocumentProcessor(
        ILogger<TestDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus eventBus,
        IGenerateExternalRefIdentities identityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, eventBus, identityGenerator, refs)
    {
    }

    protected override Task ProcessInternal(ProcessDocumentCommand command)
    {
        // No-op for testing
        return Task.CompletedTask;
    }

    // Expose protected method for testing
    public Task PublishDependencyRequestPublic<TParentId>(
        ProcessDocumentCommand command,
        IHasRef? hasRef,
        TParentId parentId,
        DocumentType documentType)
    {
        return PublishDependencyRequest(command, hasRef, parentId, documentType);
    }
}

