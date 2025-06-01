using SportsData.Core.Common.Routing;
using SportsData.Core.Converters;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnCollegeDto : IHasRoutingKey
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(ParseStringToLongConverter))]
        public long Id { get; set; }

        [JsonPropertyName("guid")]
        public Guid Guid { get; set; }

        [JsonPropertyName("mascot")]
        public string Mascot { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("shortName")]
        public string ShortName { get; set; }

        [JsonPropertyName("abbrev")]
        public string Abbrev { get; set; }

        [JsonPropertyName("logos")]
        public List<EspnImageDto> Logos { get; set; }

        public string RoutingKey { get; } = "espn.v2.colleges";
    }
}