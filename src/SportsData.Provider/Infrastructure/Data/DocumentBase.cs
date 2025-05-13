using System.Text.Json.Serialization;
using SportsData.Core.Common;

namespace SportsData.Provider.Infrastructure.Data
{
    public class DocumentBase
    {
        [JsonPropertyName("id")] // or JsonProperty if using Newtonsoft
        public string Id { get; set; }

        public string? CanonicalId { get; set; }

        public string Data { get; set; }

        public Sport Sport { get; set; }

        public DocumentType DocumentType { get; set; }

        public SourceDataProvider SourceDataProvider { get; set; }
    }
}
