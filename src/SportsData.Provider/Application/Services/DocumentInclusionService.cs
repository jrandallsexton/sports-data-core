using SportsData.Core.Extensions;
using System.Net;

namespace SportsData.Provider.Application.Services
{
    /// <summary>
    /// Determines whether to include document JSON in event payloads based on size constraints.
    /// Azure Service Bus has message size limits that must be respected.
    /// </summary>
    public interface IDocumentInclusionService
    {
        /// <summary>
        /// Determines if the JSON document should be included in the event payload
        /// or if only a reference should be sent (requiring the consumer to fetch it).
        /// Automatically decodes HTML-encoded JSON from document stores.
        /// </summary>
        /// <param name="json">The JSON document to evaluate (may be HTML-encoded)</param>
        /// <returns>The decoded JSON if it fits within size limits, null if it exceeds limits</returns>
        string? GetIncludableJson(string json);

        /// <summary>
        /// Decodes HTML-encoded JSON from document stores (Cosmos/MongoDB).
        /// </summary>
        /// <param name="json">The potentially HTML-encoded JSON</param>
        /// <returns>Decoded JSON string</returns>
        string? DecodeJson(string json);

        /// <summary>
        /// Checks if the JSON document size exceeds the maximum inline size limit.
        /// </summary>
        /// <param name="json">The JSON document to check</param>
        /// <returns>True if document exceeds limit, false otherwise</returns>
        bool ExceedsSizeLimit(string json);

        /// <summary>
        /// Gets the size of the JSON document in bytes.
        /// </summary>
        /// <param name="json">The JSON document to measure</param>
        /// <returns>Size in bytes</returns>
        int GetDocumentSize(string json);

        /// <summary>
        /// Gets the maximum allowed inline JSON size in bytes.
        /// </summary>
        int MaxInlineJsonBytes { get; }
    }

    public class DocumentInclusionService : IDocumentInclusionService
    {
        private readonly ILogger<DocumentInclusionService> _logger;

        // Azure Service Bus limits:
        // - Standard tier: 256 KB max message size
        // - Premium tier: 1 MB max message size
        // Using conservative 200 KB limit (204,800 bytes) to allow for overhead
        // and other event properties (correlationId, causationId, metadata, etc.)
        private const int MAX_INLINE_JSON_BYTES = 204_800; // 200 KB

        public DocumentInclusionService(ILogger<DocumentInclusionService> logger)
        {
            _logger = logger;
        }

        public int MaxInlineJsonBytes => MAX_INLINE_JSON_BYTES;

        public string? DecodeJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                // Decode HTML entities that Cosmos/MongoDB may have encoded
                // Common encodings: &lt; &gt; &quot; &amp; &#39;
                var decoded = WebUtility.HtmlDecode(json);

                return decoded;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, 
                    "Failed to decode JSON document. Returning original. Length={Length}",
                    json.Length);
                return json;
            }
        }

        public string? GetIncludableJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            // First, decode the JSON from any HTML encoding
            var decodedJson = DecodeJson(json);
            
            if (string.IsNullOrEmpty(decodedJson))
            {
                return null;
            }

            var jsonSizeInBytes = decodedJson.GetSizeInBytes();

            if (jsonSizeInBytes <= MAX_INLINE_JSON_BYTES)
            {
                return decodedJson;
            }

            _logger.LogInformation(
                "Document JSON size ({SizeKB:F2} KB) exceeds {MaxKB} KB limit. Sending reference only, consumer will need to fetch document.",
                jsonSizeInBytes / 1024.0,
                MAX_INLINE_JSON_BYTES / 1024);

            return null;
        }

        public bool ExceedsSizeLimit(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            var jsonSizeInBytes = json.GetSizeInBytes();
            return jsonSizeInBytes > MAX_INLINE_JSON_BYTES;
        }

        public int GetDocumentSize(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return 0;
            }

            return json.GetSizeInBytes();
        }
    }
}
