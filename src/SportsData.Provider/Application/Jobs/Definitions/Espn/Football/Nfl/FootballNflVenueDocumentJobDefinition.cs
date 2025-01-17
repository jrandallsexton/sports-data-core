using SportsData.Core.Common;

namespace SportsData.Provider.Application.Jobs.Definitions.Espn.Football.Nfl
{
    public class FootballNflVenueDocumentJobDefinition : DocumentJobDefinition
    {
        public override Sport Sport { get; init; } =
            Sport.FootballNfl;

        public override SourceDataProvider SourceDataProvider { get; init; } =
            SourceDataProvider.Espn;

        public override DocumentType DocumentType { get; init; } =
            DocumentType.Venue;

        public override string Endpoint { get; init; } =
            "http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/venues?lang=en&limit=999";

        public override string EndpointMask { get; init; } =
            "http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/venues/";

        public override int? SeasonYear { get; init; } = null;
    }
}
