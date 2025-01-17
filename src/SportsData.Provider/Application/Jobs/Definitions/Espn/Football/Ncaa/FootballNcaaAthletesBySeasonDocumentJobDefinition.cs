using SportsData.Core.Common;

namespace SportsData.Provider.Application.Jobs.Definitions.Espn.Football.Ncaa
{
    public class FootballNcaaAthletesBySeasonDocumentJobDefinition : DocumentJobDefinition
    {
        public override Sport Sport { get; init; } =
            Sport.FootballNcaa;

        public override SourceDataProvider SourceDataProvider { get; init; } =
            SourceDataProvider.Espn;

        public override DocumentType DocumentType { get; init; } =
            DocumentType.CoachBySeason;

        public override string Endpoint { get; init; } =
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes?lang=en&limit=100000";

        public override string EndpointMask { get; init; } =
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/";

        public override int? SeasonYear { get; init; } = null;
    }
}
