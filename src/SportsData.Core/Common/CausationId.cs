using System;

namespace SportsData.Core.Common
{
    public static class CausationId
    {
        public static class Api
        {
            public static Guid ContestScoringProcessor = new Guid("10000000-2000-0000-0000-000000000001");
        }

        public static class Producer
        {
            public static Guid AthleteDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000001");
            public static Guid AthletePositionDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000002");
            public static Guid AthleteSeasonDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000003");
            public static Guid CoachDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000004");
            public static Guid CoachSeasonDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000005");
            public static Guid CompetitionService = new Guid("10000A00-0000-0000-0000-000000000006");
            public static Guid ContestEnrichmentProcessor = new Guid("10000000-0000-0000-0000-000000000006");
            public static Guid ContestUpdateProcessor = new Guid("10000000-0000-0000-0000-00000000000F");
            public static Guid EventCompetitionCompetitorDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000007");
            public static Guid EventCompetitionCompetitorLineScoreDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000008");
            public static Guid EventCompetitionCompetitorScoreDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000009");
            public static Guid EventCompetitionDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000000A");
            public static Guid EventCompetitionLeadersDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000000B");
            public static Guid EventCompetitionPowerIndexDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000000C");
            public static Guid EventCompetitionProbabilityDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000000D");
            public static Guid EventCompetitionStatusDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000000E");
            public static Guid EventDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000000F");
            public static Guid FootballSeasonRankingDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000010");
            public static Guid FranchiseDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000011");
            public static Guid FranchiseSeasonCreated = new Guid("10000000-0000-0000-0000-000000000012");
            public static Guid FranchiseSeasonEnrichmentProcessor = new Guid("10000001-0000-0000-0000-00000000001F");
            public static Guid GroupSeasonDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000013");
            public static Guid ImageRequestedProcessor = new Guid("10000000-0000-0000-0000-000000000014");
            public static Guid PositionDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000015");
            public static Guid SeasonDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000016");
            public static Guid SeasonPollDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000017");
            public static Guid SeasonTypeDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000018");
            public static Guid SeasonTypeWeekDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000019");
            public static Guid SeasonTypeWeekRankingsDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000001A");
            public static Guid TeamSeasonDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000001B");
            public static Guid TeamSeasonRecordDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000001C");
            public static Guid VenueCreatedDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000001D");
            public static Guid VenueDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000001E");
            public static Guid EventCompetitionPlayDocumentProcessor = new Guid("10000200-0000-0000-0000-00000000001E");
            public static Guid EventCompetitionSituationDocumentProcessor = new Guid("10000400-0000-0000-0000-00000000001E");
        }


        public static class Provider
        {
            public static Guid ResourceIndexItemProcessor = new Guid("20000000-0000-0000-0000-000000000001");
        }
    }
}
