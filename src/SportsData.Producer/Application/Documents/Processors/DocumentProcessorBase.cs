using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Documents.Processors;

/// <summary>
/// Base class for document processors that provides common functionality for publishing child document requests.
/// Eliminates duplicate boilerplate code across 20+ document processors.
/// </summary>
/// <typeparam name="TDataContext">The type of data context (BaseDataContext, TeamSportDataContext, etc.)</typeparam>
public abstract class DocumentProcessorBase<TDataContext> : IProcessDocuments
    where TDataContext : BaseDataContext
{
    protected readonly ILogger _logger;
    protected readonly TDataContext _dataContext;
    protected readonly IEventBus _publishEndpoint;
    protected readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    protected DocumentProcessorBase(
        ILogger logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
    }

    /// <summary>
    /// Each processor implements its own document processing logic.
    /// </summary>
    public abstract Task ProcessAsync(ProcessDocumentCommand command);

    /// <summary>
    /// Helper method to publish a DocumentRequested event for a child document.
    /// Eliminates 10+ lines of boilerplate per child document request.
    /// </summary>
    /// <param name="command">The parent document processing command (provides correlation context)</param>
    /// <param name="linkDto">The ESPN link DTO containing the $ref to the child document</param>
    /// <param name="parentId">The parent entity ID (will be converted to string)</param>
    /// <param name="documentType">The type of child document being requested</param>
    /// <param name="causationId">The causation ID identifying which processor is requesting the document</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected async Task PublishChildDocumentRequest<TParentId>(
        ProcessDocumentCommand command,
        EspnLinkDto? linkDto,
        TParentId parentId,
        DocumentType documentType,
        Guid causationId)
    {
        if (linkDto?.Ref is null)
        {
            _logger.LogDebug(
                "⏭️ SKIP_CHILD_DOCUMENT: No reference found for child document. " +
                "ParentId={ParentId}, ChildDocumentType={ChildDocumentType}",
                parentId,
                documentType);
            return;
        }

        ExternalRefIdentity identity;
        Uri uri;

        try
        {
            identity = _externalRefIdentityGenerator.Generate(linkDto.Ref);
            uri = new Uri(identity.CleanUrl);
        }
        catch (UriFormatException ex)
        {
            _logger.LogError(ex,
                "❌ INVALID_CHILD_URI: Failed to parse URI for child document. " +
                "ParentId={ParentId}, ChildDocumentType={ChildDocumentType}, InvalidUrl={InvalidUrl}",
                parentId,
                documentType,
                linkDto.Ref?.ToString() ?? "null");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ IDENTITY_GENERATION_FAILED: Failed to generate identity for child document. " +
                "ParentId={ParentId}, ChildDocumentType={ChildDocumentType}, Ref={Ref}",
                parentId,
                documentType,
                linkDto.Ref?.ToString() ?? "null");
            return;
        }

        _logger.LogInformation(
            "📤 PUBLISH_CHILD_REQUEST: Publishing DocumentRequested for child document. " +
            "ParentId={ParentId}, ChildDocumentType={ChildDocumentType}, ChildUrl={ChildUrl}, UrlHash={UrlHash}",
            parentId,
            documentType,
            identity.CleanUrl,
            identity.UrlHash);

        await _publishEndpoint.Publish(new DocumentRequested(
            Id: identity.UrlHash,
            ParentId: parentId?.ToString() ?? string.Empty,
            Uri: uri,
            Sport: command.Sport,
            SeasonYear: command.Season,
            DocumentType: documentType,
            SourceDataProvider: command.SourceDataProvider,
            CorrelationId: command.CorrelationId,
            CausationId: causationId
        ));

        _logger.LogDebug(
            "✅ CHILD_REQUEST_PUBLISHED: DocumentRequested published successfully. " +
            "ChildDocumentType={ChildDocumentType}, UrlHash={UrlHash}",
            documentType,
            identity.UrlHash);
    }
}
