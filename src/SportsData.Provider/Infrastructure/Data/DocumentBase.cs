
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
    }
}
