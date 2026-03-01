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
                
                // Publish completion notification if Provider requested it (for saga orchestration)
                if (command.NotifyOnCompletion)
                {
                    await PublishCompletionNotification(command);
                }
                
                _logger.LogInformation("{ProcessorName} completed.", GetType().Name);
            }
            catch (ExternalDocumentNotSourcedException retryEx)
            {
                _logger.LogWarning(retryEx,
                    "{ProcessorName} dependency not ready (attempt {Attempt}). Will retry later.",
                    GetType().Name, command.AttemptCount + 1);

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
                _logger.LogError(ex, "{ProcessorName} failed.", GetType().Name);
                throw;
            }
        }
    }

    /// <summary>
    /// Each processor implements its own document processing logic.
    /// This is where the actual work happens - deserialization, entity creation/update, child spawning, etc.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>IMPORTANT:</strong> Implementations must NOT catch or swallow <see cref="ExternalDocumentNotSourcedException"/>.
    /// This exception must be allowed to escape to the base <see cref="ProcessAsync"/> method so that the retry logic can
    /// properly handle missing dependencies. When a dependency is not ready, throw <see cref="ExternalDocumentNotSourcedException"/>
    /// and the base class will automatically schedule a retry (up to <c>MaxRetryCount</c> retries after the initial attempt).
    /// </para>
    /// <para>
    /// Other exceptions should be thrown normally and will be logged and propagated by the base class.
    /// </para>
    /// </remarks>
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
    /// Attempts to get the ParentId from the command, or derives it from the source URI using the provided mapper function.
    /// This enables processors to handle both explicit ParentId (direct invocation) and missing ParentId (dependency sourcing).
    /// </summary>
    /// <param name="command">The document processing command containing ParentId and SourceUri</param>
    /// <param name="uriMapper">Function to map the child resource URI to its parent resource URI</param>
    /// <returns>The ParentId as a Guid, or null if ParentId cannot be parsed and uriMapper is null</returns>
    /// <remarks>
    /// Use this when your processor requires a parent entity ID but the ParentId may not always be provided.
    /// Pass the appropriate EspnUriMapper function (e.g., TeamSeasonStatisticsRefToTeamSeasonRef) as the uriMapper parameter.
    /// </remarks>
    protected Guid? TryGetOrDeriveParentId(
        ProcessDocumentCommand command,
        Func<Uri, Uri>? uriMapper = null)
    {
        // First try to parse the provided ParentId
        if (Guid.TryParse(command.ParentId, out var parentId))
        {
            return parentId;
        }

        // If no mapper provided, can't derive - return null
        if (uriMapper == null)
        {
            return null;
        }

        // If SourceUri is null, can't derive - return null
        if (command.SourceUri == null)
        {
            return null;
        }

        try
        {
            // Derive parent URI and generate canonical ID
            var parentUri = uriMapper(command.SourceUri);
            var derivedId = _externalRefIdentityGenerator.Generate(parentUri).CanonicalId;

            _logger.LogDebug(
                "ParentId not provided, derived from URI. " +
                "SourceUri={SourceUri}, ParentUri={ParentUri}, DerivedId={DerivedId}",
                command.SourceUri, parentUri, derivedId);

            return derivedId;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "Failed to derive ParentId from URI. " +
                "SourceUri={SourceUri}",
                command.SourceUri);
            return null;
        }
    }

    /// <summary>
    /// Helper method to publish a DocumentRequested event for a dependency document.
    /// Dependency documents are required BEFORE processing can complete (e.g., Franchise before TeamSeason).
    /// Tracks specific dependencies (by DocumentType + UrlHash) to prevent duplicate requests on retries
    /// while still allowing new dependencies discovered on retry attempts to be requested.
    /// </summary>
    /// <param name="command">The parent document processing command (provides correlation context)</param>
    /// <param name="hasRef">The ESPN link DTO containing the $ref to the dependency document</param>
    /// <param name="parentId">The parent entity ID (will be converted to string)</param>
    /// <param name="documentType">The type of dependency document being requested</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected async Task PublishDependencyRequest<TParentId>(
        ProcessDocumentCommand command,
        IHasRef? hasRef,
        TParentId parentId,
        DocumentType documentType)
    {
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["DependencyDocumentType"] = documentType,
            ["ParentId"] = parentId?.ToString()
        }))
        {
            if (hasRef?.Ref is null)
            {
                _logger.LogInformation("⏭️ SKIP_DEPENDENCY: No reference found.");
                return;
            }

            // Generate identity to get the UrlHash for tracking
            ExternalRefIdentity identity;
            try
            {
                identity = _externalRefIdentityGenerator.Generate(hasRef.Ref);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "❌ IDENTITY_GENERATION_FAILED: Cannot track dependency without UrlHash. Ref={Ref}",
                    hasRef.Ref?.ToString() ?? "null");
                return;
            }

            var dependencyKey = new RequestedDependency(documentType, identity.UrlHash);

            // Check if we've already requested this specific dependency
            if (command.RequestedDependencies.Contains(dependencyKey))
            {
                _logger.LogInformation(
                    "⏭️ SKIP_DEPENDENCY: Already requested on previous attempt. AttemptCount={AttemptCount}",
                    command.AttemptCount);
                return;
            }

            // Publish dependency request - track only after successful publish to allow retries if publish fails
            // Pass precomputed identity to avoid redundant Generate call
            var published = await PublishDocumentRequestInternal(command, hasRef, parentId, documentType, "DEPENDENCY", identity);

            // Only track when the publish actually occurred — early returns (URI/identity failure) must not mark as done
            if (published)
                command.RequestedDependencies.Add(dependencyKey);
        }
    }

    /// <summary>
    /// Helper method to publish a DocumentRequested event for a child document.
    /// Child documents are spawned AFTER successful processing (e.g., TeamSeason spawning Venue, Statistics).
    /// Publishes on every attempt since child spawning only happens when processing succeeds past dependencies.
    /// </summary>
    /// <param name="command">The parent document processing command (provides correlation context)</param>
    /// <param name="hasRef">The ESPN link DTO containing the $ref to the child document</param>
    /// <param name="parentId">The parent entity ID (will be converted to string)</param>
    /// <param name="documentType">The type of child document being requested</param>
    /// <returns>A task representing the asynchronous operation</returns>
    protected async Task PublishChildDocumentRequest<TParentId>(
        ProcessDocumentCommand command,
        IHasRef? hasRef,
        TParentId parentId,
        DocumentType documentType)
    {
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["ChildDocumentType"] = documentType,
            ["ParentId"] = parentId?.ToString()
        }))
        {
            if (hasRef?.Ref is null)
            {
                _logger.LogDebug("⏭️ SKIP_CHILD_DOCUMENT: No reference found.");
                return;
            }

            await PublishDocumentRequestInternal(command, hasRef, parentId, documentType, "CHILD", precomputedIdentity: null);
        }
    }

    /// <summary>
    /// Internal helper to publish DocumentRequested events. Shared by both dependency and child request methods.
    /// </summary>
    /// <param name="precomputedIdentity">Optional precomputed identity to avoid redundant Generate call (used by PublishDependencyRequest)</param>
    private async Task<bool> PublishDocumentRequestInternal<TParentId>(
        ProcessDocumentCommand command,
        IHasRef hasRef,
        TParentId parentId,
        DocumentType documentType,
        string requestType,
        ExternalRefIdentity? precomputedIdentity = null)
    {
        ExternalRefIdentity identity;
        Uri uri;

        try
        {
            // Use precomputed identity if provided, otherwise generate it
            identity = precomputedIdentity ?? _externalRefIdentityGenerator.Generate(hasRef.Ref);
            uri = new Uri(identity.CleanUrl);
        }
        catch (UriFormatException ex)
        {
            _logger.LogError(ex,
                "❌ INVALID_URI: Failed to parse URI. InvalidUrl={InvalidUrl}",
                hasRef.Ref?.ToString() ?? "null");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ IDENTITY_GENERATION_FAILED: Failed to generate identity. Ref={Ref}",
                hasRef.Ref?.ToString() ?? "null");
            return false;
        }

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

        _logger.LogInformation(
            "✅ {RequestType}_REQUEST_PUBLISHED: DocumentRequested published. UrlHash={UrlHash}",
            requestType,
            identity.UrlHash);

        return true;
    }

    /// <summary>
    /// Publishes DocumentProcessingCompleted event to notify Provider saga of successful document processing.
    /// This is a simple messenger - no logic, just publish if flag is set.
    /// </summary>
    private async Task PublishCompletionNotification(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object?> { ["Step"] = "CompletionNotification" }))
        {
            await _publishEndpoint.Publish(new DocumentProcessingCompleted(
                command.CorrelationId,
                command.DocumentType,
                command.UrlHash,
                DateTimeOffset.UtcNow,
                command.Sport,
                command.Season,
                command.SourceDataProvider));

            _logger.LogInformation("📢 COMPLETION_NOTIFICATION: Enqueued to outbox.");

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("📢 COMPLETION_NOTIFICATION: Outbox flushed.");
        }
    }
}
