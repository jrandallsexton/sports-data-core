using System;
using Newtonsoft.Json;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnLinkDto
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }
    }
}
