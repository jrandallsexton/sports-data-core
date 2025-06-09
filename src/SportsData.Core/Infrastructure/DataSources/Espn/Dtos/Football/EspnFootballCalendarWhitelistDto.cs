#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Common.Routing;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballCalendarWhitelistDto : IHasRoutingKey
    {
        [JsonPropertyName("dates")]
        public List<string> Dates { get; set; }

        public string RoutingKey { get; } = "espn.v2.sports.football.leagues.college-football.calendar.whitelist";
    }
}