using SportsData.Core.Common;

namespace SportsData.Provider.Application.Jobs.Definitions.Espn.Football.Ncaa
{
    public class EspnDocumentJobTeamSeasonDefinition : DocumentJobDefinition
    {
        public override Sport Sport { get; init; } =
            Sport.FootballNcaa;

        public override SourceDataProvider SourceDataProvider { get; init; } =
            SourceDataProvider.Espn;

        public override DocumentType DocumentType { get; init; } =
            DocumentType.TeamBySeason;

        // TODO: Get the year in there
        public override string Endpoint { get; init; } =
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams?lang=en&limit=999";

        public override string EndpointMask { get; init; } =
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/";

        public override int? SeasonYear { get; init; }
    }
}