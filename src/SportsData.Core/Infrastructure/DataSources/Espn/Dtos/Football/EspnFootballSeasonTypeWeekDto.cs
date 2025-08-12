#pragma warning disable CS8618

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballSeasonTypeWeekDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("startDate")]
        public string StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public string EndDate { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("rankings")]
        public EspnLinkDto Rankings { get; set; }
    }
}
