﻿using System.Text.Json.Serialization;

namespace SportsData.Api.Application.UI.Picks.PicksPage
{
    public class SubmitUserPickRequest
    {
        [JsonPropertyName("pickemGroupId")]
        public Guid PickemGroupId { get; set; }

        [JsonPropertyName("contestId")]
        public Guid ContestId { get; set; }

        [JsonPropertyName("week")]
        public int Week { get; set; }

        [JsonPropertyName("pickType")]
        public UserPickType PickType { get; set; } = UserPickType.StraightUp;

        [JsonPropertyName("franchiseSeasonId")]
        public Guid? FranchiseSeasonId { get; set; }

        [JsonPropertyName("overUnder")]
        public OverUnderPick? OverUnder { get; set; }

        [JsonPropertyName("confidencePoints")]
        public int? ConfidencePoints { get; set; }

        [JsonPropertyName("tiebreakerGuessTotal")]
        public int? TiebreakerGuessTotal { get; set; }

        [JsonPropertyName("tiebreakerGuessHome")]
        public int? TiebreakerGuessHome { get; set; }

        [JsonPropertyName("tiebreakerGuessAway")]
        public int? TiebreakerGuessAway { get; set; }
    }
}