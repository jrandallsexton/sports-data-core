using MassTransit;

using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Infrastructure.Providers.Espn;

using System.Text.Json;

namespace SportsData.Provider.Application.Documents;

public class DocumentRequestedHandler : IConsumer<DocumentRequested>
{
    private readonly IProvideEspnApiData _espnApi;
    private readonly ILogger<DocumentRequestedHandler> _logger;
    private readonly IPublishEndpoint _publisher;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;

    public DocumentRequestedHandler(
        IProvideEspnApiData espnApi,
        ILogger<DocumentRequestedHandler> logger,
        IPublishEndpoint publisher,
        IProcessResourceIndexItems resourceIndexItemProcessor,
        IProvideBackgroundJobs backgroundJobProvider)
    {
        _espnApi = espnApi;
        _logger = logger;
        _publisher = publisher;
        _backgroundJobProvider = backgroundJobProvider;
    }

    public async Task Consume(ConsumeContext<DocumentRequested> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Handling DocumentRequested: {Msg}", msg);

        var json = await _espnApi.GetResource(msg.Uri);

        using var doc = JsonDocument.Parse(json);

        // If it's an index (including hybrids), process refs only
        if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            _logger.LogInformation("Document is an index (or hybrid) with {Count} items. Processing $ref only.", items.GetArrayLength());

            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("$ref", out var refProp)) continue;

                var href = refProp.GetString();
                if (string.IsNullOrWhiteSpace(href)) continue;

                if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
                {
                    _logger.LogWarning("Skipping invalid $ref href: {Ref}", href);
                    continue;
                }

                var id = uri.Segments.LastOrDefault()?.TrimEnd('/');
                if (string.IsNullOrWhiteSpace(id)) continue;

                var routingKey = HashProvider.GenerateHashFromUri(uri).Substring(0, 3).ToUpperInvariant();

                await _publisher.Publish(new DocumentRequested(
                    id,
                    msg.ParentId,
                    uri,
                    msg.Sport,
                    msg.SeasonYear,
                    msg.DocumentType,
                    msg.SourceDataProvider,
                    msg.CorrelationId,
                    msg.CausationId));

            }

            return; // Never persist hybrid/index documents
        }

        // If it's not an index — treat it as a leaf document
        _logger.LogInformation("Document is a leaf (non-index). Forwarding to ResourceIndexItemProcessor.");

        var urlHash = HashProvider.GenerateHashFromUri(msg.Uri);

        // TODO: Create a default ResourceIndex
        var cmd = new ProcessResourceIndexItemCommand(
            ResourceIndexId: Guid.Empty,
            Id: urlHash,
            Uri: msg.Uri,
            Sport: msg.Sport,
            SourceDataProvider: msg.SourceDataProvider,
            DocumentType: msg.DocumentType,
            ParentId: msg.ParentId,
            SeasonYear: msg.SeasonYear);

        _backgroundJobProvider.Enqueue<IProcessResourceIndexItems>(p => p.Process(cmd));
    }

}
