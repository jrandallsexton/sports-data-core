using MassTransit;

using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Infrastructure.Providers.Espn;

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
        var msg = context.Message;
        _logger.LogInformation("Handling DocumentRequested: {Msg}", msg);

        var uri = msg.Uri;
        var seenPages = new HashSet<string>();
        var enqueuedAnyRefs = false;

        while (uri is not null && seenPages.Add(uri.ToString()))
        {
            string json;

            try
            {
                // Fetch the first version from cache
                json = await _espnApi.GetResource(uri, bypassCache: false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch resource at {Uri}. Aborting pagination at this point.", uri);
                break;
            }

            EspnResourceIndexDto? dto;
            try
            {
                dto = json.FromJson<EspnResourceIndexDto>();

                // If it's a ResourceIndex, re-fetch uncached and re-parse
                if (dto?.Items is { Count: > 0 })
                {
                    _logger.LogInformation("Detected ResourceIndex at {Uri}. Re-fetching without cache.", uri);
                    json = await _espnApi.GetResource(uri, bypassCache: true, stripQuerystring: false);
                    dto = json.FromJson<EspnResourceIndexDto>();
                }
            }
            catch (JsonException)
            {
                _logger.LogDebug("Not a ResourceIndex. Will treat as a leaf document: {Uri}", uri);
                dto = null;
            }

            // If we did not get a valid ResourceIndex, treat as leaf and exit
            if (dto?.Items is not { Count: > 0 })
            {
                _logger.LogInformation("No items found in resource index at {Uri}", uri);
                break;
            }

            _logger.LogInformation("Found {Count} items in resource index at page {PageIndex}/{PageCount}", dto.Count, dto.PageIndex, dto.PageCount);

            foreach (var item in dto.Items)
            {
                if (item.Ref is null)
                {
                    _logger.LogWarning("Skipping item with null ref in page {PageIndex}", dto.PageIndex); // TODO: THUR - investigate
                    continue;
                }

                var refUri = item.Ref.ToCleanUri();
                var refHash = HashProvider.GenerateHashFromUri(refUri);

                var cmd = new ProcessResourceIndexItemCommand(
                    ResourceIndexId: Guid.Empty,
                    Id: refHash,
                    Uri: refUri,
                    Sport: msg.Sport,
                    SourceDataProvider: msg.SourceDataProvider,
                    DocumentType: msg.DocumentType,
                    ParentId: msg.ParentId,
                    SeasonYear: msg.SeasonYear);

                _backgroundJobProvider.Enqueue<IProcessResourceIndexItems>(p => p.Process(cmd));
                enqueuedAnyRefs = true;
            }

            if (dto.PageIndex >= dto.PageCount)
            {
                _logger.LogInformation("Last page reached ({PageIndex}/{PageCount})", dto.PageIndex, dto.PageCount);
                break;
            }

            var nextPage = dto.PageIndex + 1;
            var baseUri = msg.Uri.GetLeftPart(UriPartial.Path);
            uri = new Uri($"{baseUri}?limit={dto.PageSize}&page={nextPage}");
        }


        if (enqueuedAnyRefs)
        {
            _logger.LogInformation("All resource index items queued. No need to persist index document.");
            return;
        }

        // Treat as a leaf document
        _logger.LogInformation("No refs found. Treating {Uri} as a leaf document.", uri);

        var urlHash = HashProvider.GenerateHashFromUri(uri!);

        var leafCmd = new ProcessResourceIndexItemCommand(
            ResourceIndexId: Guid.Empty,
            Id: urlHash,
            Uri: uri!,
            Sport: msg.Sport,
            SourceDataProvider: msg.SourceDataProvider,
            DocumentType: msg.DocumentType,
            ParentId: msg.ParentId,
            SeasonYear: msg.SeasonYear);

        _backgroundJobProvider.Enqueue<IProcessResourceIndexItems>(p => p.Process(leafCmd));
    }

    private static bool IsResourceIndex(Uri uri)
    {
        // Strip trailing slash if present
        var last = uri.Segments.Last().TrimEnd('/');

        // If the last segment parses to a number → it's an item (leaf).
        // Otherwise → it's an index.
        return !long.TryParse(last, out _);
    }

}
