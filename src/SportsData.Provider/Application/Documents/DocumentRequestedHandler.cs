using MassTransit;

using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Processors;

namespace SportsData.Provider.Application.Documents;

public class DocumentRequestedHandler : IConsumer<DocumentRequested>
{
    private readonly IProvideEspnApiData _espnApi;
    private readonly ILogger<DocumentRequestedHandler> _logger;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;

    public DocumentRequestedHandler(
        IProvideEspnApiData espnApi,
        ILogger<DocumentRequestedHandler> logger,
        IProvideBackgroundJobs backgroundJobProvider)
    {
        _espnApi = espnApi;
        _logger = logger;
        _backgroundJobProvider = backgroundJobProvider;
    }

    public async Task Consume(ConsumeContext<DocumentRequested> context)
    {
        var evt = context.Message;

        // ✅ Validate CorrelationId - generate new one if empty
        if (evt.CorrelationId == Guid.Empty)
        {
            _logger.LogWarning(
                "DocumentRequested received with empty CorrelationId. Generating new one. Uri={Uri}, DocumentType={DocumentType}",
                evt.Uri,
                evt.DocumentType);
            
            // Generate new correlation ID from current Activity (OpenTelemetry)
            var newCorrelationId = ActivityExtensions.GetCorrelationId();
            
            // Create new event with valid CorrelationId
            evt = new DocumentRequested(
                evt.Id,
                evt.ParentId,
                evt.Uri,
                evt.Ref,
                evt.Sport,
                evt.SeasonYear,
                evt.DocumentType,
                evt.SourceDataProvider,
                newCorrelationId,  // ✅ Use new correlation ID
                evt.CausationId,
                evt.PropertyBag,
                evt.IncludeLinkedDocumentTypes);
        }

        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = evt.CorrelationId,
                   ["DocumentType"] = evt.DocumentType,
                   ["Uri"] = evt.Uri.ToString()
               }))
        {
            _logger.LogInformation(
                "DocumentRequested received. Uri={Uri}, DocumentType={DocumentType}, Sport={Sport}, Provider={Provider}",
                evt.Uri,
                evt.DocumentType,
                evt.Sport,
                evt.SourceDataProvider);

            try
            {
                await ConsumeInternal(evt);
                
                _logger.LogInformation("DocumentRequested processed. Uri={Uri}", evt.Uri);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DocumentRequested processing failed. Uri={Uri}", evt.Uri);
                throw;
            }
        }
    }

    private async Task ConsumeInternal(DocumentRequested evt)
    {
        var uri = evt.Uri;

        if (EspnResourceIndexClassifier.IsResourceIndex(uri))
        {
            _logger.LogInformation(
                "Treating as resource index. Uri={Uri}, CorrelationId={CorrelationId}",
                uri,
                evt.CorrelationId);
            await ProcessResourceIndex(uri, evt);
        }
        else
        {
            _logger.LogInformation(
                "Treating as leaf document. Uri={Uri}, CorrelationId={CorrelationId}",
                uri,
                evt.CorrelationId);
            ProcessResourceIndexItem(uri, evt);
        }
    }

    private void ProcessResourceIndexItem(Uri uri, DocumentRequested evt)
    {
        var urlHash = HashProvider.GenerateHashFromUri(uri);

        var cmd = new ProcessResourceIndexItemCommand(
            CorrelationId: evt.CorrelationId,
            CausationId: evt.CausationId,
            ResourceIndexId: Guid.Empty,
            Id: urlHash,
            Uri: uri,
            Sport: evt.Sport,
            SourceDataProvider: evt.SourceDataProvider,
            DocumentType: evt.DocumentType,
            ParentId: evt.ParentId,
            SeasonYear: evt.SeasonYear,
            BypassCache: true, // TODO: I cannot think of a reason where would ever want a cached document here.
            IncludeLinkedDocumentTypes: evt.IncludeLinkedDocumentTypes);

        _logger.LogInformation(
            "Enqueuing ProcessResourceIndexItem. UrlHash={UrlHash}, DocumentType={DocumentType}, CorrelationId={CorrelationId}",
            urlHash,
            evt.DocumentType,
            evt.CorrelationId);
            
        _backgroundJobProvider.Enqueue<IProcessResourceIndexItems>(p => p.Process(cmd));
    }

    private async Task ProcessResourceIndex(Uri uri, DocumentRequested evt)
    {
        var seenPages = new HashSet<string>();
        var enqueuedAnyRefs = false;
        var totalItemsEnqueued = 0;

        while (uri is not null && seenPages.Add(uri.ToString()))
        {
            string json;

            try
            {
                _logger.LogInformation(
                    "Fetching resource index page. Uri={Uri}, CorrelationId={CorrelationId}",
                    uri,
                    evt.CorrelationId);

                json = await _espnApi.GetResource(uri, bypassCache: true, stripQuerystring: false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to fetch resource index. Uri={Uri}, CorrelationId={CorrelationId}",
                    uri,
                    evt.CorrelationId);
                break;
            }

            EspnResourceIndexDto? dto;
            try
            {
                dto = json.FromJson<EspnResourceIndexDto>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to parse ResourceIndex JSON. Uri={Uri}, CorrelationId={CorrelationId}",
                    uri,
                    evt.CorrelationId);
                break;
            }

            if (dto == null || dto.Items == null || dto.Items.Count == 0)
            {
                _logger.LogWarning(
                    "Empty ResourceIndex. Uri={Uri}, CorrelationId={CorrelationId}",
                    uri,
                    evt.CorrelationId);
                break;
            }

            _logger.LogInformation(
                "Found items in resource index page. Count={Count}, PageIndex={PageIndex}, PageCount={PageCount}, CorrelationId={CorrelationId}",
                dto.Count,
                dto.PageIndex,
                dto.PageCount,
                evt.CorrelationId);

            foreach (var item in dto.Items)
            {
                if (item.Ref is null)
                {
                    _logger.LogDebug(
                        "Skipping item with null ref. PageIndex={PageIndex}, CorrelationId={CorrelationId}",
                        dto.PageIndex,
                        evt.CorrelationId);
                    continue;
                }

                var refUri = item.Ref.ToCleanUri();
                var refHash = HashProvider.GenerateHashFromUri(refUri);

                var cmd = new ProcessResourceIndexItemCommand(
                    CorrelationId: evt.CorrelationId,
                    CausationId: evt.CausationId,
                    ResourceIndexId: Guid.Empty,
                    Id: refHash,
                    Uri: refUri,
                    Sport: evt.Sport,
                    SourceDataProvider: evt.SourceDataProvider,
                    DocumentType: evt.DocumentType,
                    ParentId: evt.ParentId,
                    SeasonYear: evt.SeasonYear,
                    BypassCache: true,
                    IncludeLinkedDocumentTypes: evt.IncludeLinkedDocumentTypes);

                _backgroundJobProvider.Enqueue<IProcessResourceIndexItems>(p => p.Process(cmd));
                enqueuedAnyRefs = true;
                totalItemsEnqueued++;
            }

            if (dto.PageIndex >= dto.PageCount)
            {
                _logger.LogInformation(
                    "Last page reached. PageIndex={PageIndex}, PageCount={PageCount}, CorrelationId={CorrelationId}",
                    dto.PageIndex,
                    dto.PageCount,
                    evt.CorrelationId);
                break;
            }

            var nextPage = dto.PageIndex + 1;

            var originalQuery = System.Web.HttpUtility.ParseQueryString(uri.Query);
            originalQuery["page"] = nextPage.ToString();
            originalQuery["limit"] = dto.PageSize.ToString();

            var baseUri = uri.GetLeftPart(UriPartial.Path);
            var newQuery = originalQuery.ToString(); // re-encoded querystring
            uri = new Uri($"{baseUri}?{newQuery}");
        }

        if (enqueuedAnyRefs)
        {
            _logger.LogInformation(
                "All resource index items enqueued. TotalItems={TotalItems}, DocumentType={DocumentType}, CorrelationId={CorrelationId}",
                totalItemsEnqueued,
                evt.DocumentType,
                evt.CorrelationId);
        }
        else
        {
            _logger.LogWarning(
                "No resource index items enqueued. DocumentType={DocumentType}, CorrelationId={CorrelationId}",
                evt.DocumentType,
                evt.CorrelationId);
        }
    }
}
