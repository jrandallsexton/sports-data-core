using MassTransit;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Processors;

using System.Text.Json;

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
            MessageId: evt.MessageId,
            ResourceIndexId: Guid.Empty,
            Id: urlHash,
            Uri: uri,
            Sport: evt.Sport,
            SourceDataProvider: evt.SourceDataProvider,
            DocumentType: evt.DocumentType,
            ParentId: evt.ParentId,
            SeasonYear: evt.SeasonYear,
            BypassCache: false, // Check MongoDB first before calling ESPN (critical for historical sourcing)
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
        bool? useInlineJson = null; // null = not yet probed, true = $refs are broken, false = $refs work

        while (uri is not null && seenPages.Add(uri.ToString()))
        {
            _logger.LogInformation(
                "Fetching resource index page. Uri={Uri}, CorrelationId={CorrelationId}",
                uri,
                evt.CorrelationId);

            var result = await _espnApi.GetResource(uri, bypassCache: true, stripQuerystring: false);

            if (!result.IsSuccess)
            {
                _logger.LogError(
                    "Failed to fetch resource index: Status={Status}, Uri={Uri}, CorrelationId={CorrelationId}",
                    result.Status,
                    uri,
                    evt.CorrelationId);

                return; // Early exit on failure
            }

            var json = result.Value;

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
                _logger.LogInformation(
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

            // On first page, detect hybrid and probe first $ref to see if individual items resolve
            List<string>? rawItemJsonList = null;
            if (useInlineJson is null)
            {
                (useInlineJson, rawItemJsonList) = await DetectBrokenHybridAsync(dto, json, evt.CorrelationId);
            }

            // Extract raw item JSON strings from the page response if we need inline data
            if (useInlineJson == true && rawItemJsonList is null)
            {
                rawItemJsonList = ExtractRawItemJsonArray(json);
            }

            if (useInlineJson == true && (rawItemJsonList is null || rawItemJsonList.Count != dto.Items.Count))
            {
                _logger.LogWarning(
                    "Failed to extract inline JSON items (count mismatch). Falling back to $ref fetching. Uri={Uri}, CorrelationId={CorrelationId}",
                    uri, evt.CorrelationId);
                useInlineJson = false;
                rawItemJsonList = null;
            }

            for (var i = 0; i < dto.Items.Count; i++)
            {
                var item = dto.Items[i];
                Uri refUri;

                if (item.Ref is null)
                {
                    // Handle items without $ref by checking for Id and constructing filtered URI
                    if (string.IsNullOrWhiteSpace(item.Id))
                    {
                        _logger.LogDebug(
                            "Skipping item with null ref and no id. PageIndex={PageIndex}, CorrelationId={CorrelationId}",
                            dto.PageIndex,
                            evt.CorrelationId);
                        continue;
                    }

                    // Construct filtered URI preserving existing query params: {baseUri}?id={itemId}&...
                    var itemQuery = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    itemQuery["id"] = System.Web.HttpUtility.UrlEncode(item.Id);

                    var itemBaseUri = uri.GetLeftPart(UriPartial.Path);
                    var itemQueryString = itemQuery.ToString(); // re-encoded querystring
                    refUri = new Uri($"{itemBaseUri}?{itemQueryString}");

                    _logger.LogDebug(
                        "Item has no ref but has id. Constructed filtered URI. Id={ItemId}, Uri={Uri}, CorrelationId={CorrelationId}",
                        item.Id,
                        refUri,
                        evt.CorrelationId);
                }
                else
                {
                    refUri = item.Ref.ToCleanUri();
                }

                var refHash = HashProvider.GenerateHashFromUri(refUri);

                var cmd = new ProcessResourceIndexItemCommand(
                    CorrelationId: evt.CorrelationId,
                    CausationId: evt.CausationId,
                    MessageId: evt.MessageId,
                    ResourceIndexId: Guid.Empty,
                    Id: refHash,
                    Uri: refUri,
                    Sport: evt.Sport,
                    SourceDataProvider: evt.SourceDataProvider,
                    DocumentType: evt.DocumentType,
                    ParentId: evt.ParentId,
                    SeasonYear: evt.SeasonYear,
                    BypassCache: true,
                    IncludeLinkedDocumentTypes: evt.IncludeLinkedDocumentTypes,
                    InlineJson: useInlineJson == true ? rawItemJsonList![i] : null);

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
            _logger.LogInformation(
                "No resource index items enqueued. DocumentType={DocumentType}, CorrelationId={CorrelationId}",
                evt.DocumentType,
                evt.CorrelationId);
        }
    }

    /// <summary>
    /// Detects whether a resource index is a "broken hybrid" — items have $ref URIs that return 404
    /// but contain full inline data. Probes the first item's $ref to determine this.
    /// </summary>
    private async Task<(bool isBrokenHybrid, List<string>? rawItems)> DetectBrokenHybridAsync(
        EspnResourceIndexDto dto, string json, Guid correlationId)
    {
        var firstItem = dto.Items[0];

        // If the first item has no $ref, this is already handled by the existing null-ref path
        if (firstItem.Ref is null)
            return (false, null);

        // Check if the response contains inline data beyond just $ref and id
        // by examining the raw JSON for the first item
        var rawItems = ExtractRawItemJsonArray(json);
        if (rawItems is null || rawItems.Count == 0)
            return (false, null);

        // Count properties in first item — if it only has $ref (and maybe id), it's not a hybrid
        using var itemDoc = JsonDocument.Parse(rawItems[0]);
        var propertyCount = itemDoc.RootElement.EnumerateObject().Count();
        if (propertyCount <= 2)
            return (false, null); // Just $ref and possibly id — standard resource index

        // This looks like a hybrid (has inline data). Probe the first $ref to see if it resolves.
        _logger.LogInformation(
            "Hybrid resource index detected (items have inline data). Probing first $ref to check if individual items resolve. Ref={Ref}, CorrelationId={CorrelationId}",
            firstItem.Ref,
            correlationId);

        var probeUri = firstItem.Ref.ToCleanUri();
        var probeResult = await _espnApi.GetResource(probeUri, bypassCache: true);

        if (probeResult is null || probeResult.IsSuccess)
        {
            _logger.LogInformation(
                "Hybrid probe succeeded — individual $refs resolve. Will fetch each item individually. CorrelationId={CorrelationId}",
                correlationId);
            return (false, null);
        }

        if (probeResult.Status == ResultStatus.NotFound)
        {
            _logger.LogWarning(
                "Hybrid probe returned 404 — individual $refs are broken. Will use inline JSON for all items. Ref={Ref}, CorrelationId={CorrelationId}",
                firstItem.Ref,
                correlationId);
            return (true, rawItems);
        }

        // Non-404 failure (rate limited, server error, etc.) — don't assume broken, try normal path
        _logger.LogWarning(
            "Hybrid probe returned {Status} — inconclusive, falling back to $ref fetching. Ref={Ref}, CorrelationId={CorrelationId}",
            probeResult.Status,
            firstItem.Ref,
            correlationId);
        return (false, null);
    }

    /// <summary>
    /// Extracts each item from the raw JSON "items" array as individual JSON strings.
    /// </summary>
    private static List<string>? ExtractRawItemJsonArray(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var itemsElement) ||
                itemsElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var result = new List<string>();
            foreach (var item in itemsElement.EnumerateArray())
            {
                result.Add(item.GetRawText());
            }
            return result;
        }
        catch
        {
            return null;
        }
    }
}
