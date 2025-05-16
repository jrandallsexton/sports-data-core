
using SportsData.Core.Common;

using System.Text.Json.Serialization;

namespace SportsData.Provider.Infrastructure.Data
{
    public class DocumentBase
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
    }
}
