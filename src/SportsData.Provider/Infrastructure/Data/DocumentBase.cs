
using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;

using System.Text.Json.Serialization;

namespace SportsData.Provider.Infrastructure.Data
{
    public class DocumentBase : IHasSourceUrl
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonIgnore]
        public string id => Id;

        public string? CanonicalId { get; set; }

        public string Data { get; set; }

        public Sport Sport { get; set; }

        public DocumentType DocumentType { get; set; }

        public SourceDataProvider SourceDataProvider { get; set; }

        public string UrlHash { get; set; }

        public string Url { get; set; }

        [JsonPropertyName("_etag")]
        public string? ETag { get; set; }  // Required for concurrency
    }
}
