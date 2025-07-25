using System;

namespace SportsData.Core.Common
{
    public static class CausationId
    {
        public static class Producer
        {
            public static Guid AthleteDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000001");
            public static Guid AthletePositionDocumentProcessor = new Guid("10000000-0000-0000-0000-00000000000B");

            public static Guid EventDocumentProcessor = new Guid("10000000-0000-0000-0000-000100000002");
            public static Guid EventCompetitionDocumentProcessor = new Guid("10000000-0500-0000-0000-000100000002");

            public static Guid FranchiseDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000002");
            public static Guid FranchiseSeasonCreated = new Guid("10000000-0000-0000-0000-000000000003");

            public static Guid VenueCreatedDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000004");
            public static Guid VenueDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000005");

            public static Guid GroupBySeasonDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000006");
            public static Guid PositionDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000007");

            public static Guid TeamSeasonDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000008");
            public static Guid TeamSeasonRecordDocumentProcessor = new Guid("10000000-0000-0000-0000-000000000009");

            public static Guid ImageRequestedProcessor = new Guid("10000000-0000-0000-0000-00000000000A");
        }

        public static class Provider
        {
            public static Guid ResourceIndexItemProcessor = new Guid("20000000-0000-0000-0000-000000000001");
        }
    }
}