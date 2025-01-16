using SportsData.Core.Common;

namespace SportsData.Provider.Application.Jobs.Definitions.Espn.Football.Ncaa
{
    public class EspnDocumentJobFranchiseDefinition : DocumentJobDefinition
    {
        public override Sport Sport { get; init; } =
            Sport.FootballNcaa;

        public override SourceDataProvider SourceDataProvider { get; init; } =
            SourceDataProvider.Espn;

        public override DocumentType DocumentType { get; init; } =
            DocumentType.Franchise;

        public override string Endpoint { get; init; } =
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises?lang=en&limit=999";

        public override string EndpointMask { get; init; } =
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/";

        public override int? SeasonYear { get; init; } = null;
    }
}
