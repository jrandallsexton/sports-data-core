using Newtonsoft.Json;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnImageDto
    {
        [JsonProperty("href")]
        public Uri Href { get; set; }

        [JsonProperty("width")]
        public long Width { get; set; }

        [JsonProperty("height")]
        public long Height { get; set; }

        [JsonProperty("alt")]
        public string Alt { get; set; }

        [JsonProperty("rel")]
        public List<string> Rel { get; set; }
    }
}
