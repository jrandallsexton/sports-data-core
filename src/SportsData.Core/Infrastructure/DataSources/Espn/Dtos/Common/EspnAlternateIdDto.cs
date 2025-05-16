
using SportsData.Core.Converters;

using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnAlternateIdDto
    {
        [JsonPropertyName("sdr")]
        [JsonConverter(typeof(ParseStringToLongConverter))]
        public long Sdr { get; set; }
    }
}
