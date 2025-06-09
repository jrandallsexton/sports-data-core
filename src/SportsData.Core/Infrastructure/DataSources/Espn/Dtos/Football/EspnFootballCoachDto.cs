#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Common.Routing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballCoachDto : IHasRoutingKey
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("uid")]
        public string Uid { get; set; }

        [JsonPropertyName("firstName")]
        public string FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string LastName { get; set; }

        [JsonPropertyName("experience")]
        public int Experience { get; set; }

        [JsonPropertyName("careerRecords")]
        public List<EspnLinkDto> CareerRecords { get; set; }

        [JsonPropertyName("coachSeasons")]
        public List<EspnLinkDto> CoachSeasons { get; set; }

        // Unique routing key to avoid collision with EspnFootballCoachesDto
        public string RoutingKey { get; } = "espn.v2.sports.football.leagues.college-football.coach";
    }
}