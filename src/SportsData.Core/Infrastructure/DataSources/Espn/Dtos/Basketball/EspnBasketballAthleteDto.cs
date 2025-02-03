using Newtonsoft.Json;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Basketball
{
    public class EspnBasketballAthleteDto : EspnAthleteDto
    {
        [JsonProperty("debutYear")]
        public int DebutYear { get; set; }

        [JsonProperty("contract")]
        public EspnBasketballAthleteContractDto Contract { get; set; }

        [JsonProperty("draft")]
        public EspnAthleteDraftDto Draft { get; set; }

        //TODO: Finish mapping this
        public class EspnBasketballAthleteContractDto
        {
            [JsonProperty("birdStatus")]
            public int BirdStatus { get; set; }

            [JsonProperty("incomingTradeValue")]
            public int IncomingTradeValue { get; set; }

            [JsonProperty("outgoingTradeValue")]
            public int OutgoingTradeValue { get; set; }

            [JsonProperty("salary")]
            public int Salary { get; set; }

            [JsonProperty("salaryRemaining")]
            public int SalaryRemaining { get; set; }

            [JsonProperty("yearsRemaining")]
            public int YearsRemaining { get; set; }
        }
    }
}
