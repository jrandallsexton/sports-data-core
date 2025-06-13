#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Converters;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    /// <summary>
    /// Represents a college or university entity as defined by ESPN's data model.
    /// http://sports.core.api.espn.com/v2/colleges/99
    /// </summary>
    /// <remarks>This data transfer object (DTO) is used to encapsulate information about a college or
    /// university, including its unique identifiers, name, mascot, abbreviation, and associated logos.</remarks>
    public class EspnCollegeDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

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
    }
}