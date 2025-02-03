using Newtonsoft.Json;

using SportsData.Core.Converters;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnVenueDto
    {
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }

        [JsonProperty("id")]
        [System.Text.Json.Serialization.JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("fullName")]
        public string FullName { get; set; }

        [JsonProperty("shortName")]
        public string ShortName { get; set; }

        [JsonProperty("address")]
        public EspnAddressDto Address { get; set; }

        [JsonProperty("capacity")]
        public long Capacity { get; set; }

        [JsonProperty("grass")]
        public bool Grass { get; set; }

        [JsonProperty("indoor")]
        public bool Indoor { get; set; }

        [JsonProperty("images")]
        public List<EspnImageDto> Images { get; set; }
    }
}
