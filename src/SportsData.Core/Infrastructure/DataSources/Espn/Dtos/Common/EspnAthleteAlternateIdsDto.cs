using Newtonsoft.Json;
using SportsData.Core.Converters;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    public class EspnAthleteAlternateIdsDto
    {
        [JsonProperty("sdr")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Sdr { get; set; }
    }
}
