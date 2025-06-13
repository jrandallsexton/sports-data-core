#pragma warning disable CS8618 // Non-nullable property is uninitialized

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common
{
    // http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/drives
    // Note - this is a hybrid ResourceIndex and ResourceIndexItem

    public class EspnEventCompetitionDriveDto : IHasRef
    {
        [JsonPropertyName("$ref")]
        public Uri Ref { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("pageIndex")]
        public int PageIndex { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("pageCount")]
        public int PageCount { get; set; }

        [JsonPropertyName("items")]
        public List<EspnEventCompetitionDriveItemDto> Items { get; set; }
    }
}
