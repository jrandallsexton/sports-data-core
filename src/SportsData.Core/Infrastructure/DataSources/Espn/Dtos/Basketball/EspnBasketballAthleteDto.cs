using System.Text.Json.Serialization;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Basketball
{
    public class EspnBasketballAthleteDto : EspnAthleteDto
    {
        [JsonPropertyName("debutYear")]
        public int DebutYear { get; set; }

        [JsonPropertyName("contract")]
        public EspnBasketballAthleteContractDto Contract { get; set; }

        [JsonPropertyName("draft")]
        public EspnAthleteDraftDto Draft { get; set; }

        public class EspnBasketballAthleteContractDto
        {
            [JsonPropertyName("birdStatus")]
            public int BirdStatus { get; set; }

            [JsonPropertyName("incomingTradeValue")]
            public int IncomingTradeValue { get; set; }

            [JsonPropertyName("outgoingTradeValue")]
            public int OutgoingTradeValue { get; set; }

            [JsonPropertyName("salary")]
            public int Salary { get; set; }

            [JsonPropertyName("salaryRemaining")]
            public int SalaryRemaining { get; set; }

            [JsonPropertyName("yearsRemaining")]
            public int YearsRemaining { get; set; }
        }
    }
}