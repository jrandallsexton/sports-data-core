using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
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
    protected readonly IGenerateResourceRefs _refGenerator;

    protected DocumentProcessorBase(
        ILogger logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refGenerator
        )
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
        _refGenerator = refGenerator;
    }

    /// <summary>
    /// Template method that handles logging scope, entry/completion/error logging.
    /// Concrete processors implement ProcessInternal() for their specific logic.
    /// Can be overridden for special cases (e.g., retry handling), but most processors won't need to.
    /// </summary>
    public virtual async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(command.ToLogScope()))
        {
            _logger.LogInformation("{ProcessorName} started.", GetType().Name);

            try
            {
                await ProcessInternal(command);
                _logger.LogInformation("{ProcessorName} completed.", GetType().Name);
            }
            catch (ExternalDocumentNotSourcedException retryEx)
            {
                _logger.LogWarning(retryEx, "{ProcessorName} dependency not ready. Will retry later.", GetType().Name);

                var docCreated = command.ToDocumentCreated(command.AttemptCount + 1);

                var headers = new Dictionary<string, object>
                {
                    ["RetryReason"] = retryEx.Message
                };

                await _publishEndpoint.Publish(docCreated, headers);
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ProcessorName} failed. {@SafeCommand}", GetType().Name, command.ToSafeLogObject());
                throw;
            }
        }
    }

    /// <summary>
    /// Each processor implements its own document processing logic.
    /// This is where the actual work happens - deserialization, entity creation/update, child spawning, etc.
    /// </summary>
    protected abstract Task ProcessInternal(ProcessDocumentCommand command);

    /// <summary>
    /// Determines if a linked document of the specified type should be spawned,
    /// based on the inclusion filter in the command.
    /// </summary>
    /// <param name="documentType">The type of linked document to check</param>
    /// <param name="command">The processing command containing the optional inclusion filter</param>
    /// <returns>True if the document should be spawned; false otherwise</returns>
    protected bool ShouldSpawn(DocumentType documentType, ProcessDocumentCommand command)
    {
        // If no inclusion filter is specified, spawn all documents (default behavior)
        if (command.IncludeLinkedDocumentTypes == null || command.IncludeLinkedDocumentTypes.Count == 0)
        {
            return true;
        }

        // If inclusion filter is specified, only spawn if the type is in the list
        var shouldSpawn = command.IncludeLinkedDocumentTypes.Contains(documentType);

        if (!shouldSpawn)
        {
            _logger.LogInformation(
                "Skipping spawn of {DocumentType} due to inclusion filter. Allowed types: {AllowedTypes}",
                documentType,
                string.Join(", ", command.IncludeLinkedDocumentTypes));
        }

        return shouldSpawn;
    }

    /// <summary>
    /// Helper method to publish a DocumentRequested event for a child document.
    /// Eliminates 10+ lines of boilerplate per child document request.
    /// </summary>
    /// <param name="command">The parent document processing command (provides correlation context)</param>
    /// <param name="linkDto">The ESPN link DTO containing the $ref to the child document</param>
    /// <param name="parentId">The parent entity ID (will be converted to string)</param>
    /// <param name="documentType">The type of child document being requested</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected async Task PublishChildDocumentRequest<TParentId>(
        ProcessDocumentCommand command,
        IHasRef? hasRef,
        TParentId parentId,
        DocumentType documentType)
    {
        if (hasRef?.Ref is null)
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
            identity = _externalRefIdentityGenerator.Generate(hasRef.Ref);
            uri = new Uri(identity.CleanUrl);
        }
        catch (UriFormatException ex)
        {
            _logger.LogError(ex,
                "❌ INVALID_CHILD_URI: Failed to parse URI for child document. " +
                "ParentId={ParentId}, ChildDocumentType={ChildDocumentType}, InvalidUrl={InvalidUrl}",
                parentId,
                documentType,
                hasRef.Ref?.ToString() ?? "null");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ IDENTITY_GENERATION_FAILED: Failed to generate identity for child document. " +
                "ParentId={ParentId}, ChildDocumentType={ChildDocumentType}, Ref={Ref}",
                parentId,
                documentType,
                hasRef.Ref?.ToString() ?? "null");
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
            Id: identity.CanonicalId.ToString(),
            ParentId: parentId?.ToString() ?? null,
            Uri: uri,
            Ref: null,
            Sport: command.Sport,
            SeasonYear: command.Season,
            DocumentType: documentType,
            SourceDataProvider: command.SourceDataProvider,
            CorrelationId: command.CorrelationId,
            CausationId: command.MessageId
        ));

        _logger.LogDebug(
            "✅ CHILD_REQUEST_PUBLISHED: DocumentRequested published successfully. " +
            "ChildDocumentType={ChildDocumentType}, UrlHash={UrlHash}",
            documentType,
            identity.UrlHash);
    }
}
