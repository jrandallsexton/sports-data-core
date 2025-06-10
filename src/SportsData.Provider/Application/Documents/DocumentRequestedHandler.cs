using MassTransit;

using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Infrastructure.Providers.Espn;

using System.Text.Json;

namespace SportsData.Provider.Application.Handlers;

public class DocumentRequestedHandler : IConsumer<DocumentRequested>
{
    private readonly IProvideEspnApiData _espnApi;
    private readonly ILogger<DocumentRequestedHandler> _logger;
    private readonly IPublishEndpoint _publisher;

    public DocumentRequestedHandler(
        IProvideEspnApiData espnApi,
        ILogger<DocumentRequestedHandler> logger,
        IPublishEndpoint publisher)
    {
        _espnApi = espnApi;
        _logger = logger;
        _publisher = publisher;
    }

    public async Task Consume(ConsumeContext<DocumentRequested> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Handling DocumentRequested: {@msg}", msg);

        var json = await _espnApi.GetResource(msg.Href, true);

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

                var routingKey = HashProvider.GenerateHashFromUrl(href).Substring(0, 3).ToUpperInvariant();

                await _publisher.Publish(new DocumentRequested(
                    id,
                    msg.ParentId,
                    href,
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

        var urlHash = HashProvider.GenerateHashFromUrl(msg.Href);

        await _publisher.Publish(new ProcessResourceIndexItemCommand(
            ResourceIndexId: Guid.Empty,
            Id: 0,
            Href: msg.Href,
            Sport: msg.Sport,
            SourceDataProvider: msg.SourceDataProvider,
            DocumentType: msg.DocumentType,
            ParentId: msg.ParentId,
            SeasonYear: msg.SeasonYear));
    }

}
