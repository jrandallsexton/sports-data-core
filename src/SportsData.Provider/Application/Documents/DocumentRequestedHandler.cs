using MassTransit;
using SportsData.Core.Common;
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

        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = evt.CorrelationId
               }))
        {
            await ConsumeInternal(evt);
        }
    }

    private async Task ConsumeInternal(DocumentRequested evt)
    {
        _logger.LogInformation("Handling DocumentRequested: {Evt}", evt);

        if (evt.DocumentType == DocumentType.EventCompetitionPlay)
        {
            _logger.LogError("PLAY requested: {URI}", evt.Uri.OriginalString);
        }

        var uri = evt.Uri;

        if (EspnResourceIndexClassifier.IsResourceIndex(uri))
        {
            await ProcessResourceIndex(uri, evt);
        }
        else
        {
            ProcessResourceIndexItem(uri, evt);
        }
    }

    private void ProcessResourceIndexItem(Uri uri, DocumentRequested evt)
    {
        var urlHash = HashProvider.GenerateHashFromUri(uri);

        var cmd = new ProcessResourceIndexItemCommand(
            CorrelationId: evt.CorrelationId,
            ResourceIndexId: Guid.Empty,
            Id: urlHash,
            Uri: uri,
            Sport: evt.Sport,
            SourceDataProvider: evt.SourceDataProvider,
            DocumentType: evt.DocumentType,
            ParentId: evt.ParentId,
            SeasonYear: evt.SeasonYear,
            BypassCache: true); // TODO: I cannot think of a reason where would ever want a cached document here.

        _logger.LogInformation("Treating {Uri} as a leaf document. Enqueuing single processing command.", uri);
        _backgroundJobProvider.Enqueue<IProcessResourceIndexItems>(p => p.Process(cmd));
    }

    private async Task ProcessResourceIndex(Uri uri, DocumentRequested evt)
    {
        var seenPages = new HashSet<string>();
        var enqueuedAnyRefs = false;

        while (uri is not null && seenPages.Add(uri.ToString()))
        {
            string json;

            try
            {
                json = await _espnApi.GetResource(uri, bypassCache: true, stripQuerystring: false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch resource index at {Uri}. Aborting.", uri);
                break;
            }

            EspnResourceIndexDto? dto;
            try
            {
                dto = json.FromJson<EspnResourceIndexDto>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse ResourceIndex JSON at {Uri}. Aborting.", uri);
                break;
            }

            if (dto == null || dto.Items == null || dto.Items.Count == 0)
            {
                _logger.LogWarning("Empty ResourceIndex at {Uri}. Nothing to enqueue.", uri);
                break;
            }

            _logger.LogInformation("Found {Count} items in resource index at page {PageIndex}/{PageCount}", dto.Count, dto.PageIndex, dto.PageCount);

            foreach (var item in dto.Items)
            {
                if (item.Ref is null)
                {
                    _logger.LogInformation("Skipping item with null ref in page {PageIndex}", dto.PageIndex);
                    continue;
                }

                var refUri = item.Ref.ToCleanUri();
                var refHash = HashProvider.GenerateHashFromUri(refUri);

                var cmd = new ProcessResourceIndexItemCommand(
                    CorrelationId: evt.CorrelationId,
                    ResourceIndexId: Guid.Empty,
                    Id: refHash,
                    Uri: refUri,
                    Sport: evt.Sport,
                    SourceDataProvider: evt.SourceDataProvider,
                    DocumentType: evt.DocumentType,
                    ParentId: evt.ParentId,
                    SeasonYear: evt.SeasonYear,
                    BypassCache: true);

                _backgroundJobProvider.Enqueue<IProcessResourceIndexItems>(p => p.Process(cmd));
                enqueuedAnyRefs = true;
            }

            if (dto.PageIndex >= dto.PageCount)
            {
                _logger.LogInformation("Last page reached ({PageIndex}/{PageCount})", dto.PageIndex, dto.PageCount);
                break;
            }

            //var nextPage = dto.PageIndex + 1;
            //var baseUri = uri.GetLeftPart(UriPartial.Path);
            //uri = new Uri($"{baseUri}?limit={dto.PageSize}&page={nextPage}");

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
            _logger.LogInformation("All resource index items queued.");
        }
    }
}
