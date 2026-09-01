#nullable enable

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
using SportsData.Producer.Exceptions;
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
        DocumentType documentType = DocumentType.TeamSeason,
        IReadOnlyCollection<DocumentType>? includeLinkedDocumentTypes = null)
    {
        return new ProcessDocumentCommand(
            sourceDataProvider: SourceDataProvider.Espn,
            sport: Sport.FootballNcaa,
            seasonYear: 2024,
            documentType: documentType,
            document: "{}",
            messageId: Guid.NewGuid(),
            correlationId: Guid.NewGuid(),
            parentId: null,
            sourceUri: new Uri("http://test.com"),
            urlHash: "test123",
            attemptCount: attemptCount,
            includeLinkedDocumentTypes: includeLinkedDocumentTypes);
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

        // Compute expected hash and verify deduplication tracking
        var expectedIdentity = generator.Generate(hasRef.Ref);
        command.RequestedDependencies.Should().ContainSingle(d => 
            d.Type == DocumentType.Franchise && 
            d.UrlHash == expectedIdentity.UrlHash);
    }

    [Fact]
    public async Task ProcessAsync_Should_Copy_RequestedDependencies_To_Retry_Event()
    {
        // Arrange
        var busMock = Mocker.GetMock<IEventBus>();
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        // Create a processor that throws ExternalDocumentNotSourcedException
        var processor = Mocker.CreateInstance<ThrowingTestDocumentProcessor<FootballDataContext>>();

        // Create command with seeded RequestedDependencies
        var command = CreateTestCommand(attemptCount: 0, documentType: DocumentType.EventCompetition);
        command.RequestedDependencies.Add(new RequestedDependency(DocumentType.Franchise, "hash1"));
        command.RequestedDependencies.Add(new RequestedDependency(DocumentType.Venue, "hash2"));

        // Act - ProcessAsync should catch the exception and publish DocumentCreated with RequestedDependencies
        await processor.ProcessAsync(command);

        // Assert - Verify DocumentCreated was published with the seeded RequestedDependencies
        busMock.Verify(x => x.Publish(
            It.Is<DocumentCreated>(e =>
                e.DocumentType == DocumentType.EventCompetition &&
                e.AttemptCount == 1 &&
                e.RequestedDependencies!.Count == 2 &&
                e.RequestedDependencies.Any(d => d.Type == DocumentType.Franchise && d.UrlHash == "hash1") &&
                e.RequestedDependencies.Any(d => d.Type == DocumentType.Venue && d.UrlHash == "hash2")),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task PublishDependencyRequest_Should_Propagate_IncludeLinkedDocumentTypes_To_Child_Request()
    {
        // Arrange
        var busMock = Mocker.GetMock<IEventBus>();
        Mocker.Use<IGenerateExternalRefIdentities>(new ExternalRefIdentityGenerator());

        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();

        var filter = new List<DocumentType>
        {
            DocumentType.EventCompetition,
            DocumentType.EventCompetitionStatus,
            DocumentType.EventCompetitionCompetitor
        };

        var command = CreateTestCommand(
            documentType: DocumentType.Event,
            includeLinkedDocumentTypes: filter);

        var hasRef = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401234567/competitions/401234567") };

        // Act
        await processor.PublishDependencyRequestPublic(command, hasRef, Guid.NewGuid(), DocumentType.EventCompetition);

        // Assert — the published DocumentRequested must carry the same filter
        // forward, otherwise downstream processors lose the narrowing intent.
        busMock.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e =>
                e.IncludeLinkedDocumentTypes != null &&
                e.IncludeLinkedDocumentTypes.Count == filter.Count &&
                e.IncludeLinkedDocumentTypes.SequenceEqual(filter)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishDependencyRequest_Should_Publish_Null_Filter_When_Command_Has_No_Filter()
    {
        // Arrange — explicit guard that the default (null filter / spawn-all) path
        // continues to work after the propagation change. A null filter must
        // serialize as null on the published event, not as an empty list.
        var busMock = Mocker.GetMock<IEventBus>();
        Mocker.Use<IGenerateExternalRefIdentities>(new ExternalRefIdentityGenerator());

        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();

        var command = CreateTestCommand(documentType: DocumentType.Event);

        var hasRef = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401234567/competitions/401234567") };

        // Act
        await processor.PublishDependencyRequestPublic(command, hasRef, Guid.NewGuid(), DocumentType.EventCompetition);

        // Assert
        busMock.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e => e.IncludeLinkedDocumentTypes == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Empty-filter ("document only") semantics ----
    // IncludeLinkedDocumentTypes has three meanings: null = spawn all (default,
    // unchanged), EMPTY = spawn nothing, non-empty = only the listed types. The
    // empty case exists because a dependency request that only satisfies a foreign
    // key (a play participant needing an AthleteSeason row) has no use for the
    // subtree beneath it (Defect B in docs/features/athlete-cascade-scoping.md).
    // These tests pin the three behaviours that make it airtight: ShouldSpawn
    // refuses, the child publish choke point refuses even for ungated call sites,
    // and the empty filter propagates onto every request the command publishes.

    [Fact]
    public void ShouldSpawn_Should_Return_True_When_Filter_Is_Null()
    {
        // The default is unchanged: no filter means spawn everything.
        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();
        var command = CreateTestCommand();

        processor.ShouldSpawnPublic(DocumentType.AthleteImage, command).Should().BeTrue();
    }

    [Fact]
    public void ShouldSpawn_Should_Return_False_When_Filter_Is_Empty()
    {
        // An empty filter is no longer collapsed into the null case: it means
        // "this document only, no children".
        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();
        var command = CreateTestCommand(includeLinkedDocumentTypes: new List<DocumentType>());

        processor.ShouldSpawnPublic(DocumentType.AthleteImage, command).Should().BeFalse();
    }

    [Fact]
    public async Task PublishChildDocumentRequest_Should_Not_Publish_When_Filter_Is_Empty()
    {
        // Several processors spawn children unconditionally when an entity is new
        // (the isNew short-circuit bypasses ShouldSpawn). The choke point must hold
        // regardless of call-site discipline.
        var busMock = Mocker.GetMock<IEventBus>();
        Mocker.Use<IGenerateExternalRefIdentities>(new ExternalRefIdentityGenerator());
        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();

        var command = CreateTestCommand(includeLinkedDocumentTypes: new List<DocumentType>());
        var hasRef = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/12345/statistics") };

        await processor.PublishChildDocumentRequestPublic(command, hasRef, Guid.NewGuid(), DocumentType.AthleteSeasonStatistics);

        busMock.Verify(x => x.Publish(
            It.IsAny<DocumentRequested>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishChildDocumentRequest_Should_Publish_When_Filter_Is_NonEmpty()
    {
        // Deliberately narrow: the choke point enforces ONLY the empty case.
        // A non-empty filter must not be enforced there — that would implicitly
        // decide the isNew-bypass question (item 3 of the design doc), which is a
        // separate behavioural change. This test pins the boundary.
        var busMock = Mocker.GetMock<IEventBus>();
        Mocker.Use<IGenerateExternalRefIdentities>(new ExternalRefIdentityGenerator());
        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();

        var command = CreateTestCommand(
            includeLinkedDocumentTypes: new List<DocumentType> { DocumentType.EventCompetition });
        var hasRef = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/12345/statistics") };

        await processor.PublishChildDocumentRequestPublic(command, hasRef, Guid.NewGuid(), DocumentType.AthleteSeasonStatistics);

        busMock.Verify(x => x.Publish(
            It.IsAny<DocumentRequested>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishDependencyRequest_Should_Still_Publish_When_Filter_Is_Empty()
    {
        // An empty filter blocks children, never FK resolution — a document-only
        // AthleteSeason command must still be able to request its missing Athlete
        // parent, or the row could never persist. The published request carries the
        // empty filter forward so the FK chain stays lean.
        var busMock = Mocker.GetMock<IEventBus>();
        Mocker.Use<IGenerateExternalRefIdentities>(new ExternalRefIdentityGenerator());
        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();

        var command = CreateTestCommand(includeLinkedDocumentTypes: new List<DocumentType>());
        var hasRef = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/12345") };

        await processor.PublishDependencyRequestPublic(command, hasRef, Guid.NewGuid(), DocumentType.Athlete);

        busMock.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e =>
                e.IncludeLinkedDocumentTypes != null &&
                e.IncludeLinkedDocumentTypes.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishDependencyRequest_Should_Apply_PerCall_Filter_Override()
    {
        // An unfiltered command (a play) can mark ONE dependency request (its
        // participant's AthleteSeason) as document-only without affecting its own
        // filter or its other requests.
        var busMock = Mocker.GetMock<IEventBus>();
        Mocker.Use<IGenerateExternalRefIdentities>(new ExternalRefIdentityGenerator());
        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();

        var command = CreateTestCommand(documentType: DocumentType.EventCompetitionPlay);
        var hasRef = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/12345") };

        await processor.PublishDependencyRequestPublic(
            command, hasRef, Guid.NewGuid(), DocumentType.AthleteSeason,
            includeLinkedDocumentTypes: Array.Empty<DocumentType>());

        command.IncludeLinkedDocumentTypes.Should().BeNull("the override must not mutate the command");
        busMock.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e =>
                e.IncludeLinkedDocumentTypes != null &&
                e.IncludeLinkedDocumentTypes.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishDependencyRequest_Should_Inherit_Parent_Filter_When_Override_Is_Null()
    {
        // A null override means "inherit", NOT "force the default null filter".
        // This is deliberate: if a caller could force null past a filtered parent,
        // one hop could WIDEN a narrowing set at the seed (e.g. Refresh Contest's
        // set), and the cascade's contract is that filters only narrow downhill.
        var busMock = Mocker.GetMock<IEventBus>();
        Mocker.Use<IGenerateExternalRefIdentities>(new ExternalRefIdentityGenerator());
        var processor = Mocker.CreateInstance<TestDocumentProcessor<FootballDataContext>>();

        var parentFilter = new List<DocumentType> { DocumentType.EventCompetitionStatus };
        var command = CreateTestCommand(
            documentType: DocumentType.Event,
            includeLinkedDocumentTypes: parentFilter);
        var hasRef = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401234567/competitions/401234567") };

        await processor.PublishDependencyRequestPublic(
            command, hasRef, Guid.NewGuid(), DocumentType.EventCompetition,
            includeLinkedDocumentTypes: null);

        busMock.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e =>
                e.IncludeLinkedDocumentTypes != null &&
                e.IncludeLinkedDocumentTypes.SequenceEqual(parentFilter)),
            It.IsAny<CancellationToken>()), Times.Once);
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
        DocumentType documentType,
        IReadOnlyCollection<DocumentType>? includeLinkedDocumentTypes = null)
    {
        return PublishDependencyRequest(command, hasRef, parentId, documentType, includeLinkedDocumentTypes);
    }

    public bool ShouldSpawnPublic(DocumentType documentType, ProcessDocumentCommand command)
    {
        return ShouldSpawn(documentType, command);
    }

    public Task PublishChildDocumentRequestPublic<TParentId>(
        ProcessDocumentCommand command,
        IHasRef? hasRef,
        TParentId parentId,
        DocumentType documentType)
    {
        return PublishChildDocumentRequest(command, hasRef, parentId, documentType);
    }
}

/// <summary>
/// Test processor that throws ExternalDocumentNotSourcedException to test retry logic.
/// </summary>
public class ThrowingTestDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    public ThrowingTestDocumentProcessor(
        ILogger<ThrowingTestDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus eventBus,
        IGenerateExternalRefIdentities identityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, eventBus, identityGenerator, refs)
    {
    }

    protected override Task ProcessInternal(ProcessDocumentCommand command)
    {
        throw new ExternalDocumentNotSourcedException("Test exception for retry");
    }
}
