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

#nullable enable

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors;

/// <summary>
/// Unit tests for DocumentProcessorBase to verify critical dependency tracking logic.
/// Uses a minimal test processor implementation to test the abstract base class.
/// </summary>
public class DocumentProcessorBaseTests : ProducerTestBase<FootballDataContext>
{
    /// <summary>
    /// Creates a test ProcessDocumentCommand with common default values.
    /// Generates new GUIDs for messageId and correlationId on each call.
    /// </summary>
    private static ProcessDocumentCommand CreateTestCommand(
        int attemptCount = 0,
        DocumentType documentType = DocumentType.TeamSeason)
    {
        return new ProcessDocumentCommand(
            sourceDataProvider: SourceDataProvider.Espn,
            sport: Sport.FootballNcaa,
            season: 2024,
            documentType: documentType,
            document: "{}",
            messageId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(),
            parentId: null,
            sourceUri: new Uri("http://test.com"),
            urlHash: "test123",
            attemptCount: attemptCount);
    }

    [Fact]
    public async Task PublishDependencyRequest_Should_Publish_On_First_Request()
    {
        // Arrange
        var busMock = Mocker.GetMock<IEventBus>();
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();

        var command = CreateTestCommand();

        var hasRef = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/2640") };

        // Act
        await processor.PublishDependencyRequestPublic(command, hasRef, Guid.NewGuid(), DocumentType.Franchise);

        // Assert
        busMock.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.Franchise),
            It.IsAny<CancellationToken>()), Times.Once);

        command.RequestedDependencies.Should().ContainSingle(d => d.Type == DocumentType.Franchise);
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

        var command = CreateTestCommand(attemptCount: 1);

        // Simulate that this dependency was already requested
        command.RequestedDependencies.Add(new RequestedDependency(DocumentType.Franchise, identity.UrlHash));

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

        var command = CreateTestCommand(attemptCount: 1, documentType: DocumentType.EventCompetition);

        // Simulate that Franchise A was already requested
        command.RequestedDependencies.Add(new RequestedDependency(DocumentType.Franchise, identityA.UrlHash));

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

        var command = CreateTestCommand(documentType: DocumentType.EventCompetition);

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

        var command = CreateTestCommand();

        var hasRef = new EspnLinkDto { Ref = null! };

        // Act
        await processor.PublishDependencyRequestPublic(command, hasRef, Guid.NewGuid(), DocumentType.Franchise);

        // Assert
        busMock.Verify(x => x.Publish(
            It.IsAny<DocumentRequested>(),
            It.IsAny<CancellationToken>()), Times.Never);

        command.RequestedDependencies.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishDependencyRequest_Should_Skip_When_HasRef_Is_Null()
    {
        // Arrange
        var busMock = Mocker.GetMock<IEventBus>();
        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();

        var command = CreateTestCommand();

        // Act - Pass null for the hasRef parameter itself
        await processor.PublishDependencyRequestPublic(command, null, Guid.NewGuid(), DocumentType.Franchise);

        // Assert
        busMock.Verify(x => x.Publish(
            It.IsAny<DocumentRequested>(),
            It.IsAny<CancellationToken>()), Times.Never);

        command.RequestedDependencies.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishDependencyRequest_Should_Not_Publish_When_Identity_Generation_Throws()
    {
        // Arrange
        var busMock = Mocker.GetMock<IEventBus>();
        var generatorMock = Mocker.GetMock<IGenerateExternalRefIdentities>();

        // Setup the mock to throw when Generate is called
        generatorMock
            .Setup(x => x.Generate(It.IsAny<Uri>()))
            .Throws(new InvalidOperationException("Identity generation failed"));

        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();

        var command = CreateTestCommand();

        var hasRef = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/2640") };

        // Act
        await processor.PublishDependencyRequestPublic(command, hasRef, Guid.NewGuid(), DocumentType.Franchise);

        // Assert - Should not publish DocumentRequested when identity generation fails
        busMock.Verify(x => x.Publish(
            It.IsAny<DocumentRequested>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // Assert - Should not add to RequestedDependencies when identity generation fails
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

