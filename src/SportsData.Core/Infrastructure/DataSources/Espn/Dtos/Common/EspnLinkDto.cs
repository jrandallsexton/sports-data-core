using System;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnLinkDto
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }
    }
}