#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnImageDto
    {
        [JsonPropertyName("href")]
        public Uri Href { get; set; }

        [JsonPropertyName("width")]
        public long? Width { get; set; }

        [JsonPropertyName("height")]
        public long? Height { get; set; }

        [JsonPropertyName("alt")]
        public string Alt { get; set; }

        [JsonPropertyName("rel")]
        public List<string> Rel { get; set; }

        [JsonPropertyName("lastUpdated")]
        public DateTime? LastUpdatedUtc { get; set; }
    }
}