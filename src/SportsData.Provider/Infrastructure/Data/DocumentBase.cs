
using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;

using System.Text.Json.Serialization;

namespace SportsData.Provider.Infrastructure.Data
{
    public class DocumentBase : IHasSourceUrl
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonIgnore]
        public string id => Id;

        public required string Data { get; set; }

        public Sport Sport { get; set; }

        public DocumentType DocumentType { get; set; }

        public SourceDataProvider SourceDataProvider { get; set; }

        public required string SourceUrlHash { get; set; }

        public required Uri Uri { get; set; }

        [JsonPropertyName("_etag")]
        public string? ETag { get; set; }  // Required for concurrency

        [JsonPropertyName("routingKey")]
        public required string RoutingKey { get; set; }

        /// <summary>
        /// SHA-256 hash of the document content at the time DocumentCreated was last published.
        /// Used to suppress redundant publishing when cached content hasn't changed.
        /// Null means the document has never been published (or was stored before this field existed).
        /// </summary>
        public string? LastPublishedContentHash { get; set; }

        /// <summary>
        /// UTC timestamp of when DocumentCreated was last published for this document.
        /// Used with LastPublishedContentHash to implement time-based cooldown: historical
        /// documents with unchanged content are suppressed only within the cooldown window,
        /// allowing re-sourcing runs to re-publish after the cooldown expires.
        /// Null means never published (same semantics as LastPublishedContentHash).
        /// </summary>
        public DateTime? LastPublishedUtc { get; set; }
    }
}
