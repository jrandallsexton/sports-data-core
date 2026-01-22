using Microsoft.Extensions.Logging;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Producer.Application.Documents.Processors;
using SportsData.Producer.Application.Documents.Processors.Commands;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors;

public class DocumentCreatedProcessorTests : ProducerTestBase<DocumentCreatedProcessor>
{
    private readonly Mock<IProvideProviders> _providerMock;
    private readonly Mock<IDocumentProcessorFactory> _factoryMock;
    private readonly Mock<IProcessDocuments> _documentProcessorMock;

    public DocumentCreatedProcessorTests()
    {
        _providerMock = Mocker.GetMock<IProvideProviders>();
        _factoryMock = Mocker.GetMock<IDocumentProcessorFactory>();
        _documentProcessorMock = new Mock<IProcessDocuments>();
    }

    #region Happy Path Tests

    [Fact]
    public async Task Process_WithInlineDocument_ProcessesSuccessfully()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(documentJson: "{\"id\":\"123\",\"name\":\"Test\"}");
        
        _factoryMock.Setup(f => f.GetProcessor(
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                DocumentAction.Created))
            .Returns(_documentProcessorMock.Object);

        _documentProcessorMock.Setup(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()))
            .Returns(Task.CompletedTask);

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.Is<ProcessDocumentCommand>(
            cmd => cmd.Document == evt.DocumentJson &&
                   cmd.DocumentType == evt.DocumentType &&
                   cmd.Sport == evt.Sport &&
                   cmd.SourceUri == evt.SourceRef &&
                   cmd.UrlHash == evt.SourceUrlHash &&
                   cmd.OriginalUri == evt.Ref)), Times.Once);

        // Verify Provider was NOT called (document was inline)
        _providerMock.Verify(p => p.GetDocumentByUrlHash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Process_WithMissingDocument_FetchesFromProvider()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(documentJson: null); // No inline document
        var providerDocument = "{\"id\":\"456\",\"name\":\"From Provider\"}";

        _providerMock.Setup(p => p.GetDocumentByUrlHash(evt.SourceUrlHash))
            .ReturnsAsync(providerDocument);

        _factoryMock.Setup(f => f.GetProcessor(
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                DocumentAction.Created))
            .Returns(_documentProcessorMock.Object);

        _documentProcessorMock.Setup(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()))
            .Returns(Task.CompletedTask);

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert
        _providerMock.Verify(p => p.GetDocumentByUrlHash(evt.SourceUrlHash), Times.Once);
        
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.Is<ProcessDocumentCommand>(
            cmd => cmd.Document == providerDocument)), Times.Once);
    }

    #endregion

    #region Invalid Document Tests

    [Fact]
    public async Task Process_WithNullInlineDocument_ReturnsEarly()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(documentJson: null);
        
        _providerMock.Setup(p => p.GetDocumentByUrlHash(evt.SourceUrlHash))
            .ReturnsAsync((string?)null);

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert - Should not call processor
        _factoryMock.Verify(f => f.GetProcessor(
            It.IsAny<SourceDataProvider>(),
            It.IsAny<Sport>(),
            It.IsAny<DocumentType>(),
            It.IsAny<DocumentAction>()), Times.Never);
        
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()), Times.Never);
    }

    [Fact]
    public async Task Process_WithEmptyInlineDocument_ReturnsEarly()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(documentJson: string.Empty);

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert - Should not call processor
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()), Times.Never);
    }

    [Fact]
    public async Task Process_WithWhitespaceInlineDocument_ReturnsEarly()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(documentJson: "   ");

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert - Should not call processor
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()), Times.Never);
    }

    [Fact]
    public async Task Process_WithLiteralNullString_ReturnsEarly()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(documentJson: "null");

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert - Should not call processor
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()), Times.Never);
    }

    [Fact]
    public async Task Process_WithLiteralNullStringUpperCase_ReturnsEarly()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(documentJson: "NULL");

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert - Should not call processor
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()), Times.Never);
    }

    [Fact]
    public async Task Process_WithLiteralNullStringWithWhitespace_ReturnsEarly()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(documentJson: "  null  ");

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert - Should not call processor
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenProviderReturnsNull_ReturnsEarly()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(documentJson: null);
        
        _providerMock.Setup(p => p.GetDocumentByUrlHash(evt.SourceUrlHash))
            .ReturnsAsync((string?)null);

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert
        _providerMock.Verify(p => p.GetDocumentByUrlHash(evt.SourceUrlHash), Times.Once);
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenProviderReturnsLiteralNull_ReturnsEarly()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(documentJson: null);
        
        _providerMock.Setup(p => p.GetDocumentByUrlHash(evt.SourceUrlHash))
            .ReturnsAsync("null");

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert
        _providerMock.Verify(p => p.GetDocumentByUrlHash(evt.SourceUrlHash), Times.Once);
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()), Times.Never);
    }

    #endregion

    #region ProcessDocumentCommand Tests

    [Fact]
    public async Task Process_PassesCorrectCommandToProcessor()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(
            documentJson: "{\"test\":\"data\"}",
            includeLinkedTypes: [DocumentType.Event, DocumentType.AthleteSeason]);
        
        _factoryMock.Setup(f => f.GetProcessor(
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                DocumentAction.Created))
            .Returns(_documentProcessorMock.Object);

        ProcessDocumentCommand? capturedCommand = null;
        _documentProcessorMock.Setup(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()))
            .Callback<ProcessDocumentCommand>(cmd => capturedCommand = cmd)
            .Returns(Task.CompletedTask);

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal(evt.SourceDataProvider, capturedCommand.SourceDataProvider);
        Assert.Equal(evt.Sport, capturedCommand.Sport);
        Assert.Equal(evt.SeasonYear, capturedCommand.Season);
        Assert.Equal(evt.DocumentType, capturedCommand.DocumentType);
        Assert.Equal(evt.DocumentJson, capturedCommand.Document);
        Assert.Equal(evt.CorrelationId, capturedCommand.CorrelationId);
        Assert.Equal(evt.ParentId, capturedCommand.ParentId);
        Assert.Equal(evt.SourceRef, capturedCommand.SourceUri);
        Assert.Equal(evt.SourceUrlHash, capturedCommand.UrlHash);
        Assert.Equal(evt.Ref, capturedCommand.OriginalUri);
        Assert.Equal(evt.AttemptCount, capturedCommand.AttemptCount);
        Assert.Equal(evt.IncludeLinkedDocumentTypes, capturedCommand.IncludeLinkedDocumentTypes);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task Process_WhenProcessorThrows_RethrowsException()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(documentJson: "{\"test\":\"data\"}");
        var expectedException = new InvalidOperationException("Test exception");

        _factoryMock.Setup(f => f.GetProcessor(
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                DocumentAction.Created))
            .Returns(_documentProcessorMock.Object);

        _documentProcessorMock.Setup(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()))
            .ThrowsAsync(expectedException);

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Process(evt));
        Assert.Equal(expectedException.Message, exception.Message);
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public async Task Process_WithComplexInclusionFilter_PassesFilterCorrectly()
    {
        // Arrange
        var includeTypes = new List<DocumentType>
        {
            DocumentType.Event,
            DocumentType.AthleteSeason,
            DocumentType.TeamSeasonStatistics
        };

        var evt = CreateDocumentCreatedEvent(
            documentJson: "{\"complex\":\"data\"}",
            includeLinkedTypes: includeTypes);

        _factoryMock.Setup(f => f.GetProcessor(
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                DocumentAction.Created))
            .Returns(_documentProcessorMock.Object);

        _documentProcessorMock.Setup(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()))
            .Returns(Task.CompletedTask);

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.Is<ProcessDocumentCommand>(
            cmd => cmd.IncludeLinkedDocumentTypes != null &&
                   cmd.IncludeLinkedDocumentTypes.Count == 3 &&
                   cmd.IncludeLinkedDocumentTypes.Contains(DocumentType.Event) &&
                   cmd.IncludeLinkedDocumentTypes.Contains(DocumentType.AthleteSeason) &&
                   cmd.IncludeLinkedDocumentTypes.Contains(DocumentType.TeamSeasonStatistics))), Times.Once);
    }

    [Fact]
    public async Task Process_WithHtmlEncodedInlineDocument_PassesEncodedDocument()
    {
        // Arrange - Document with HTML encoding (as received from Cosmos)
        var encodedDocument = "{&quot;id&quot;:&quot;333&quot;,&quot;name&quot;:&quot;LSU Tigers&quot;}";
        var evt = CreateDocumentCreatedEvent(documentJson: encodedDocument);

        _factoryMock.Setup(f => f.GetProcessor(
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                DocumentAction.Created))
            .Returns(_documentProcessorMock.Object);

        _documentProcessorMock.Setup(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()))
            .Returns(Task.CompletedTask);

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert - Document is passed as-is (decoding happens downstream)
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.Is<ProcessDocumentCommand>(
            cmd => cmd.Document == encodedDocument)), Times.Once);
    }

    [Fact]
    public async Task Process_WithLargeDocument_FetchesFromProviderAndProcesses()
    {
        // Arrange - Simulate large document scenario (not included inline)
        var evt = CreateDocumentCreatedEvent(documentJson: null);
        var largeDocument = new string('x', 300_000); // 300KB

        _providerMock.Setup(p => p.GetDocumentByUrlHash(evt.SourceUrlHash))
            .ReturnsAsync(largeDocument);

        _factoryMock.Setup(f => f.GetProcessor(
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                DocumentAction.Created))
            .Returns(_documentProcessorMock.Object);

        _documentProcessorMock.Setup(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()))
            .Returns(Task.CompletedTask);

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert
        _providerMock.Verify(p => p.GetDocumentByUrlHash(evt.SourceUrlHash), Times.Once);
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.Is<ProcessDocumentCommand>(
            cmd => cmd.Document == largeDocument)), Times.Once);
    }

    #endregion

    #region Factory Integration Tests

    [Fact]
    public async Task Process_CallsFactoryWithCorrectParameters()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(documentJson: "{\"id\":\"789\"}");

        _factoryMock.Setup(f => f.GetProcessor(
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                DocumentAction.Created))
            .Returns(_documentProcessorMock.Object);

        _documentProcessorMock.Setup(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()))
            .Returns(Task.CompletedTask);

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert
        _factoryMock.Verify(f => f.GetProcessor(
            SourceDataProvider.Espn,
            Sport.FootballNcaa,
            DocumentType.TeamSeason,
            DocumentAction.Created), Times.Once);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Process_WithZeroAttemptCount_ProcessesSuccessfully()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(
            documentJson: "{\"first\":\"attempt\"}",
            attemptCount: 0);

        _factoryMock.Setup(f => f.GetProcessor(
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                DocumentAction.Created))
            .Returns(_documentProcessorMock.Object);

        _documentProcessorMock.Setup(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()))
            .Returns(Task.CompletedTask);

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.Is<ProcessDocumentCommand>(
            cmd => cmd.AttemptCount == 0)), Times.Once);
    }

    [Fact]
    public async Task Process_WithRetryAttempt_PassesAttemptCountCorrectly()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(
            documentJson: "{\"retry\":\"attempt\"}",
            attemptCount: 3);

        _factoryMock.Setup(f => f.GetProcessor(
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                DocumentAction.Created))
            .Returns(_documentProcessorMock.Object);

        _documentProcessorMock.Setup(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()))
            .Returns(Task.CompletedTask);

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.Is<ProcessDocumentCommand>(
            cmd => cmd.AttemptCount == 3)), Times.Once);
    }

    [Fact]
    public async Task Process_WithNullSeasonYear_PassesNullSeasonCorrectly()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(
            documentJson: "{\"no\":\"season\"}",
            seasonYear: null);

        _factoryMock.Setup(f => f.GetProcessor(
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                DocumentAction.Created))
            .Returns(_documentProcessorMock.Object);

        _documentProcessorMock.Setup(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()))
            .Returns(Task.CompletedTask);

        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert
        _documentProcessorMock.Verify(p => p.ProcessAsync(It.Is<ProcessDocumentCommand>(
            cmd => cmd.Season == null)), Times.Once);
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task Process_LogsEntryAndCompletion()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(documentJson: "{\"test\":\"data\"}");
        
        _factoryMock.Setup(f => f.GetProcessor(
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                DocumentAction.Created))
            .Returns(_documentProcessorMock.Object);

        _documentProcessorMock.Setup(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()))
            .Returns(Task.CompletedTask);

        var loggerMock = Mocker.GetMock<ILogger<DocumentCreatedProcessor>>();
        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act
        await sut.Process(evt);

        // Assert - Verify entry log
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DOC_CREATED_PROCESSOR_ENTRY")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Assert - Verify completion log
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DOC_CREATED_PROCESSOR_COMPLETED")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Process_WhenExceptionThrown_LogsFailure()
    {
        // Arrange
        var evt = CreateDocumentCreatedEvent(documentJson: "{\"test\":\"data\"}");
        var expectedException = new InvalidOperationException("Test failure");

        _factoryMock.Setup(f => f.GetProcessor(
                evt.SourceDataProvider,
                evt.Sport,
                evt.DocumentType,
                DocumentAction.Created))
            .Returns(_documentProcessorMock.Object);

        _documentProcessorMock.Setup(p => p.ProcessAsync(It.IsAny<ProcessDocumentCommand>()))
            .ThrowsAsync(expectedException);

        var loggerMock = Mocker.GetMock<ILogger<DocumentCreatedProcessor>>();
        var sut = Mocker.CreateInstance<DocumentCreatedProcessor>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Process(evt));

        // Assert - Verify failure log
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DOC_CREATED_PROCESSOR_FAILED")),
                It.Is<Exception>(ex => ex == expectedException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private static DocumentCreated CreateDocumentCreatedEvent(
        string? documentJson = null,
        int attemptCount = 0,
        int? seasonYear = 2024,
        IReadOnlyCollection<DocumentType>? includeLinkedTypes = null)
    {
        return new DocumentCreated(
            Id: Guid.NewGuid().ToString(),
            ParentId: null,
            Name: "EspnTeamSeasonDto",
            SourceRef: new Uri("https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/333"),
            Ref: new Uri("https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/333"),
            DocumentJson: documentJson,
            SourceUrlHash: "abc123hash",
            Sport: Sport.FootballNcaa,
            SeasonYear: seasonYear,
            DocumentType: DocumentType.TeamSeason,
            SourceDataProvider: SourceDataProvider.Espn,
            CorrelationId: Guid.NewGuid(),
            CausationId: Guid.NewGuid(),
            AttemptCount: attemptCount,
            IncludeLinkedDocumentTypes: includeLinkedTypes);
    }

    #endregion
}
