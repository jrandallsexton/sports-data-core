using System;

namespace SportsData.Core.Common
{
    public static class CausationId
    {
        public static class Producer
        {
            public static Guid AthleteDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000001");
            public static Guid AthletePositionDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000002");
            public static Guid AthleteSeasonDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000003");
            public static Guid EventCompetitionCompetitorDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000004");
            public static Guid EventCompetitionCompetitorLineScoreDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000005");
            public static Guid EventCompetitionCompetitorScoreDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000006");
            public static Guid EventCompetitionDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000007");
            public static Guid EventCompetitionLeadersDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000008");
            public static Guid EventCompetitionPowerIndexDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000009");
            public static Guid EventCompetitionStatusDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000000A");
            public static Guid EventDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000000B");
            public static Guid FranchiseDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000000C");
            public static Guid FranchiseSeasonCreated = new Guid("10000000-0000-0000-0000-00000000000D");
            public static Guid GroupBySeasonDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000000E");
            public static Guid ImageRequestedProcessor = new Guid("10000000-0000-0000-0000-00000000000F");
            public static Guid PositionDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000010");
            public static Guid TeamSeasonDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000011");
            public static Guid TeamSeasonRecordDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000012");
            public static Guid VenueCreatedDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000013");
            public static Guid VenueDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000014");
        }

        public static class Provider
        {
            public static Guid ResourceIndexItemProcessor = new Guid("20000000-0000-0000-0000-000000000001");
        }
    }
}
